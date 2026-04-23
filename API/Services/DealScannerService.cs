using System.Text.Json;
using API.Config;
using API.Data;
using API.Entities.DealFinder;
using API.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace API.Services
{
    public class DealScannerService(
        IServiceScopeFactory scopeFactory,
        IOptions<DealScannerSettings> opts,
        IConfiguration config,
        ILogger<DealScannerService> log,
        HttpClient http) : BackgroundService
    {
        public static volatile bool IsScanning;

        private static readonly SemaphoreSlim _ebaySem = new(5);  // max 5 concurrent eBay calls

        protected override async Task ExecuteAsync(CancellationToken ct)
        {
            await Task.Delay(TimeSpan.FromSeconds(10), ct);
            while (!ct.IsCancellationRequested)
            {
                try { await RunScanCycleAsync(ct); }
                catch (OperationCanceledException) { break; }
                catch (Exception ex) { log.LogError(ex, "DealScanner cycle failed"); }

                log.LogInformation("DealScanner sleeping {M} min", opts.Value.ScanIntervalMinutes);
                await Task.Delay(TimeSpan.FromMinutes(opts.Value.ScanIntervalMinutes), ct);
            }
        }

        private async Task RunScanCycleAsync(CancellationToken ct)
        {
            IsScanning = true;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            log.LogInformation("DealScanner cycle starting");
            try
            {
                using var scope     = scopeFactory.CreateScope();
                var db              = scope.ServiceProvider.GetRequiredService<StoreContext>();
                var cache           = scope.ServiceProvider.GetRequiredService<ICacheService>();
                var ebayFinding     = scope.ServiceProvider.GetRequiredService<IEbayFindingService>();
                var settings        = opts.Value;

                // ── Step 1: Single Keepa Product Finder call (pre-filtered deals) ──
                var asins = await FetchDealAsinsAsync(cache, ct);
                log.LogInformation("DealScanner: {Count} candidate ASINs from Keepa", asins.Count);
                if (asins.Count == 0) return;

                // ── Step 2: Batch-fetch product data (10 per request) ──────────────
                var products = await FetchProductBatchesAsync(asins, cache, ct);
                log.LogInformation("DealScanner: {Count} products with valid prices", products.Count);

                // ── Step 3: Parallel eBay lookups + profit calc ──────────────────
                var processTasks = products.Select(p =>
                    ProcessProductAsync(p, db, cache, ebayFinding, settings, ct));
                await Task.WhenAll(processTasks);

                await db.SaveChangesAsync(ct);

                // ── Step 4: Mark stale deals inactive ───────────────────────────
                var cutoff = DateTime.UtcNow.AddHours(-48);
                var stale  = await db.DealFinderDeals
                    .Where(d => d.LastUpdated < cutoff && d.IsActive)
                    .ToListAsync(ct);
                stale.ForEach(d => d.IsActive = false);
                await db.SaveChangesAsync(ct);

                sw.Stop();
                log.LogInformation("DealScanner cycle complete in {Ms}ms. Stale: {Stale}", sw.ElapsedMilliseconds, stale.Count);
            }
            finally { IsScanning = false; }
        }

        // ── Keepa Product Finder — single call, returns pre-filtered ASINs ────────
        // Filters: UK domain, price £5–£200, dropped ≥10% from 90-day avg, BSR dropped ≥2 times in 30d
        private async Task<List<string>> FetchDealAsinsAsync(ICacheService cache, CancellationToken ct)
        {
            const string cacheKey = "dealfinder:keepa:deal-asins";
            var cached = await cache.GetAsync<List<string>>(cacheKey);
            if (cached is not null)
            {
                log.LogInformation("DealScanner: deal ASINs from cache ({Count})", cached.Count);
                return cached;
            }

            var apiKey = config["KeepaSettings:ApiKey"];

            // Keepa Product Finder — filters products to only those with real price drops
            var selection = new
            {
                page               = 0,
                perPage            = 50,
                sortType           = 1,          // sort by deal score
                isFilterEnabled    = true,
                filterCondition    = new
                {
                    current_NEW_lte          = 20000,  // max £200 (pence)
                    current_NEW_gte          = 500,    // min £5
                    deltaPercent_NEW_90_lte  = -10,    // price dropped ≥10% vs 90-day avg
                    salesRankDrops30_gte     = 2,      // selling well (BSR dropped 2+ times)
                    reviewCount_gte          = 5,      // has some reviews
                    domainId                 = 2,      // UK
                }
            };

            var selectionJson = Uri.EscapeDataString(JsonSerializer.Serialize(selection));
            var url           = $"https://api.keepa.com/query?key={apiKey}&domain=2&selection={selectionJson}";

            for (var attempt = 0; attempt < 3; attempt++)
            {
                try
                {
                    var response = await http.GetAsync(url, ct);

                    if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                    {
                        var waitMs = 60_000;
                        try
                        {
                            var body = await response.Content.ReadAsStringAsync(ct);
                            var doc  = JsonDocument.Parse(body);
                            if (doc.RootElement.TryGetProperty("refillIn", out var ri))
                                waitMs = Math.Max(ri.GetInt32() + 1000, 5000);
                        }
                        catch { }
                        log.LogWarning("Keepa 429 on product finder — waiting {Secs}s (attempt {A}/3)", waitMs / 1000, attempt + 1);
                        await Task.Delay(waitMs, ct);
                        continue;
                    }

                    response.EnsureSuccessStatusCode();
                    var json = await response.Content.ReadAsStringAsync(ct);
                    var doc2 = JsonDocument.Parse(json);

                    if (doc2.RootElement.TryGetProperty("tokensLeft", out var tl))
                    {
                        log.LogInformation("Keepa tokens remaining: {T}", tl.GetInt32());
                        if (tl.GetInt32() < 10)
                        {
                            log.LogWarning("Keepa tokens critically low — pausing 15 min");
                            await Task.Delay(TimeSpan.FromMinutes(15), ct);
                        }
                    }

                    if (!doc2.RootElement.TryGetProperty("asinList", out var asinArr))
                        return [];

                    var asins = asinArr.EnumerateArray()
                        .Select(a => a.GetString() ?? "")
                        .Where(a => !string.IsNullOrEmpty(a))
                        .ToList();

                    // Cache for 30 minutes — re-run frequently to catch new drops
                    await cache.SetAsync(cacheKey, asins, TimeSpan.FromMinutes(30));
                    return asins;
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex) when (attempt < 2)
                {
                    log.LogWarning(ex, "Keepa product finder error (attempt {A})", attempt + 1);
                    await Task.Delay(TimeSpan.FromSeconds(15), ct);
                }
                catch (Exception ex)
                {
                    log.LogWarning(ex, "Keepa product finder failed — skipping cycle");
                    return [];
                }
            }
            return [];
        }

        // ── Batch product data fetch (10 ASINs per request) ─────────────────────
        private async Task<List<KeepaProduct>> FetchProductBatchesAsync(
            List<string> asins, ICacheService cache, CancellationToken ct)
        {
            var results = new List<KeepaProduct>();
            var apiKey  = config["KeepaSettings:ApiKey"];

            foreach (var batch in asins.Chunk(10))
            {
                if (ct.IsCancellationRequested) break;

                var toFetch = new List<string>();
                foreach (var asin in batch)
                {
                    var cached = await cache.GetAsync<KeepaProduct>($"dealfinder:keepa:product:{asin}");
                    if (cached is not null) results.Add(cached);
                    else toFetch.Add(asin);
                }
                if (toFetch.Count == 0) continue;

                var asinParam = string.Join(",", toFetch);
                var url       = $"https://api.keepa.com/product?key={apiKey}&domain=2&asin={asinParam}&stats=180&history=0";

                for (var attempt = 0; attempt < 3; attempt++)
                {
                    try
                    {
                        var response = await http.GetAsync(url, ct);
                        if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                        {
                            var waitMs = 60_000;
                            try
                            {
                                var body = await response.Content.ReadAsStringAsync(ct);
                                var doc  = JsonDocument.Parse(body);
                                if (doc.RootElement.TryGetProperty("refillIn", out var ri))
                                    waitMs = Math.Max(ri.GetInt32() + 1000, 5000);
                            }
                            catch { }
                            log.LogWarning("Keepa 429 on product batch — waiting {Secs}s", waitMs / 1000);
                            await Task.Delay(waitMs, ct);
                            continue;
                        }

                        response.EnsureSuccessStatusCode();
                        var json = await response.Content.ReadAsStringAsync(ct);
                        var doc2 = JsonDocument.Parse(json);

                        if (doc2.RootElement.TryGetProperty("tokensLeft", out var tl) && tl.GetInt32() < 10)
                        {
                            log.LogWarning("Keepa tokens critically low — pausing 15 min");
                            await Task.Delay(TimeSpan.FromMinutes(15), ct);
                        }

                        if (!doc2.RootElement.TryGetProperty("products", out var products)) break;

                        foreach (var p in products.EnumerateArray())
                        {
                            var product = ParseKeepaProduct(p);
                            if (product is null) continue;
                            await cache.SetAsync($"dealfinder:keepa:product:{product.Asin}", product, TimeSpan.FromHours(6));
                            results.Add(product);
                        }
                        break;
                    }
                    catch (OperationCanceledException) { throw; }
                    catch (Exception ex) when (attempt < 2)
                    {
                        log.LogWarning(ex, "Keepa product batch error (attempt {A})", attempt + 1);
                        await Task.Delay(TimeSpan.FromSeconds(10), ct);
                    }
                    catch (Exception ex)
                    {
                        log.LogWarning(ex, "Keepa product batch failed for: {Asins}", asinParam);
                        break;
                    }
                }

                // Small delay between batches — respectful of rate limits
                await Task.Delay(TimeSpan.FromMilliseconds(200), ct);
            }
            return results;
        }

        // ── Process single product — runs in parallel via Task.WhenAll ───────────
        private async Task ProcessProductAsync(
            KeepaProduct product,
            StoreContext db,
            ICacheService cache,
            IEbayFindingService ebayFinding,
            DealScannerSettings settings,
            CancellationToken ct)
        {
            if (product.BuyPrice <= 0) return;

            var existing = await db.DealFinderDeals.FirstOrDefaultAsync(d => d.Asin == product.Asin, ct);
            var minAge   = TimeSpan.FromMinutes(settings.ScanIntervalMinutes / 2.0);
            if (existing is not null && (DateTime.UtcNow - existing.LastUpdated) < minAge) return;

            // eBay lookup — semaphore limits to 5 concurrent calls
            await _ebaySem.WaitAsync(ct);
            decimal ebayAvg;
            int ebaySoldCount;
            try   { (ebayAvg, ebaySoldCount) = await GetEbaySoldPriceAsync(product.Title, cache, ebayFinding, ct); }
            finally { _ebaySem.Release(); }

            if (ebayAvg <= 0) return;

            var (profit, roi, fees) = CalculateProfit(product.BuyPrice, ebayAvg, settings);
            if (profit < settings.MinProfitThresholdGbp) return;

            var priceVar    = product.AvgPrice90Days - product.AvgPrice30Days;
            var priceVarPct = product.AvgPrice90Days > 0
                ? Math.Round(priceVar / product.AvgPrice90Days * 100, 2) : 0m;

            if (existing is null)
            {
                lock (db)  // EF DbContext is not thread-safe — lock for Add
                {
                    db.DealFinderDeals.Add(new DealFinderDeal
                    {
                        Asin                    = product.Asin,
                        Title                   = product.Title,
                        ImageUrl                = product.ImageUrl,
                        Category                = product.Category,
                        Brand                   = product.Brand,
                        AmazonLikeNewPrice      = product.BuyPrice,
                        AmazonNewPrice          = product.NewPrice,
                        EbayAvgSoldPrice        = ebayAvg,
                        EbaySoldCount30Days     = ebaySoldCount,
                        EbayFees                = fees,
                        ShippingCost            = settings.ShippingCostGbp,
                        Profit                  = profit,
                        Roi                     = roi,
                        SalesRank               = product.SalesRank,
                        SalesRankDrops30        = product.SalesRankDrops30,
                        BoughtLastMonth         = 0,
                        Rating                  = product.Rating,
                        ReviewCount             = product.ReviewCount,
                        SellerType              = product.IsFba ? "FBA" : "FBM",
                        SellerCount             = product.NewOfferCount,
                        PriceVariation90To30    = priceVar,
                        PriceVariationPct90To30 = priceVarPct,
                        AmazonUrl               = $"https://www.amazon.co.uk/dp/{product.Asin}",
                        DiscoveredAt            = DateTime.UtcNow,
                        LastUpdated             = DateTime.UtcNow,
                        IsActive                = true,
                    });
                }
            }
            else
            {
                lock (db)
                {
                    existing.AmazonLikeNewPrice      = product.BuyPrice;
                    existing.AmazonNewPrice          = product.NewPrice;
                    existing.EbayAvgSoldPrice        = ebayAvg;
                    existing.EbaySoldCount30Days     = ebaySoldCount;
                    existing.EbayFees                = fees;
                    existing.Profit                  = profit;
                    existing.Roi                     = roi;
                    existing.SalesRank               = product.SalesRank;
                    existing.SalesRankDrops30        = product.SalesRankDrops30;
                    existing.Rating                  = product.Rating;
                    existing.PriceVariation90To30    = priceVar;
                    existing.PriceVariationPct90To30 = priceVarPct;
                    existing.LastUpdated             = DateTime.UtcNow;
                    existing.IsActive                = true;
                }
            }
        }

        private static (decimal profit, decimal roi, decimal fees) CalculateProfit(
            decimal buyPrice, decimal sellPrice, DealScannerSettings s)
        {
            var fvf    = Math.Round(sellPrice * (s.EbayFinalValueFeePct / 100m), 2);
            var fees   = fvf + s.EbayFixedFeeGbp;
            var profit = Math.Round(sellPrice - buyPrice - fees - s.ShippingCostGbp, 2);
            var roi    = buyPrice > 0 ? Math.Round(profit / buyPrice * 100, 2) : 0;
            return (profit, roi, fees);
        }

        private static KeepaProduct? ParseKeepaProduct(JsonElement p)
        {
            var asin  = p.TryGetProperty("asin",  out var a) ? a.GetString() ?? "" : "";
            var title = p.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "";
            if (string.IsNullOrEmpty(asin) || string.IsNullOrEmpty(title)) return null;

            decimal ToGbp(long v) => v > 0 ? Math.Round(v / 100m, 2) : 0m;

            decimal newPrice = 0, buyBoxPrice = 0, avg30 = 0, avg90 = 0;
            int newCount = 0, salesRank = 0, bsrDrops30 = 0;

            if (p.TryGetProperty("stats", out var stats))
            {
                if (stats.TryGetProperty("current", out var cur) && cur.ValueKind == JsonValueKind.Array)
                {
                    var arr = cur.EnumerateArray().Select(x => x.GetInt64()).ToArray();
                    if (arr.Length > 1) newPrice    = ToGbp(arr[1]);
                    if (arr.Length > 9) buyBoxPrice = ToGbp(arr[9]);
                }
                if (stats.TryGetProperty("avg30", out var a30) && a30.ValueKind == JsonValueKind.Array)
                {
                    var arr = a30.EnumerateArray().Select(x => x.GetInt64()).ToArray();
                    if (arr.Length > 1) avg30 = ToGbp(arr[1]);
                }
                if (stats.TryGetProperty("avg90", out var a90) && a90.ValueKind == JsonValueKind.Array)
                {
                    var arr = a90.EnumerateArray().Select(x => x.GetInt64()).ToArray();
                    if (arr.Length > 1) avg90 = ToGbp(arr[1]);
                }
                if (stats.TryGetProperty("salesRankDrops30", out var drops))
                    bsrDrops30 = drops.GetInt32();
            }

            var buyPrice = newPrice > 0 ? newPrice : buyBoxPrice;
            if (buyPrice <= 0) return null;

            if (p.TryGetProperty("new", out var nc)) newCount = nc.GetInt32();

            string? category = null;
            if (p.TryGetProperty("salesRanks", out var ranks) && ranks.ValueKind == JsonValueKind.Object)
            {
                foreach (var rank in ranks.EnumerateObject())
                {
                    if (rank.Value.ValueKind == JsonValueKind.Array)
                    {
                        var arr = rank.Value.EnumerateArray().ToArray();
                        if (arr.Length >= 2) salesRank = arr[^1].GetInt32();
                        break;
                    }
                }
            }

            if (p.TryGetProperty("categoryTree", out var tree) && tree.ValueKind == JsonValueKind.Array)
            {
                var first = tree.EnumerateArray().FirstOrDefault();
                if (first.ValueKind == JsonValueKind.Object && first.TryGetProperty("name", out var cn))
                    category = cn.GetString();
            }

            string? imageUrl = null;
            if (p.TryGetProperty("imagesCSV", out var img) && img.GetString() is { } imgs && imgs.Length > 0)
                imageUrl = $"https://images-na.ssl-images-amazon.com/images/I/{imgs.Split(',')[0]}";

            decimal rating = p.TryGetProperty("rating",      out var rat) ? Math.Round(rat.GetDecimal() / 10m, 1) : 0;
            int reviews    = p.TryGetProperty("reviewCount", out var rev) ? rev.GetInt32() : 0;
            string? brand  = p.TryGetProperty("brand",       out var br)  ? br.GetString() : null;
            var isFba      = p.TryGetProperty("buyBoxIsFBA", out var fba) && fba.GetBoolean();

            return new KeepaProduct(asin, title, buyPrice, newPrice, imageUrl, brand, category, rating,
                reviews, salesRank, bsrDrops30, newCount, avg30, avg90, isFba);
        }

        private static async Task<(decimal avg, int soldCount)> GetEbaySoldPriceAsync(
            string title, ICacheService cache, IEbayFindingService ebayFinding, CancellationToken ct)
        {
            var keyword  = title.Length > 60 ? title[..60] : title;
            var cacheKey = $"dealfinder:ebaysold:{keyword.GetHashCode()}";

            var cachedAvg   = await cache.GetAsync<decimal?>($"{cacheKey}:avg");
            var cachedCount = await cache.GetAsync<int?>($"{cacheKey}:count");
            if (cachedAvg.HasValue && cachedCount.HasValue)
                return (cachedAvg.Value, cachedCount.Value);

            try
            {
                var result = await ebayFinding.GetCompletedSoldPricesAsync(keyword, ct);
                await cache.SetAsync($"{cacheKey}:avg",   result.AveragePrice, TimeSpan.FromHours(2));
                await cache.SetAsync($"{cacheKey}:count", result.SoldCount,    TimeSpan.FromHours(2));
                return (result.AveragePrice, result.SoldCount);
            }
            catch { return (0, 0); }
        }

        private record KeepaProduct(
            string   Asin,
            string   Title,
            decimal  BuyPrice,
            decimal  NewPrice,
            string?  ImageUrl,
            string?  Brand,
            string?  Category,
            decimal  Rating,
            int      ReviewCount,
            int      SalesRank,
            int      SalesRankDrops30,
            int      NewOfferCount,
            decimal  AvgPrice30Days,
            decimal  AvgPrice90Days,
            bool     IsFba);
    }
}