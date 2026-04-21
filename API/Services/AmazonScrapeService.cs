using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using API.DTOs;
using API.Services.Interfaces;
using Microsoft.Extensions.Configuration;

namespace API.Services;

public class AmazonScrapeService : IAmazonScrapeService
{
    private readonly IHttpClientFactory _http;
    private readonly ICacheService _cache;
    private readonly string _apiKey;
    private readonly string _baseUrl;
    private readonly string _amazonDomain;

    public AmazonScrapeService(IHttpClientFactory http, IConfiguration config, ICacheService cache)
    {
        _http         = http;
        _cache        = cache;
        _apiKey       = config["RainforestApi:ApiKey"] ?? "";
        _baseUrl      = config["RainforestApi:BaseUrl"] ?? "https://api.rainforestapi.com/request";
        _amazonDomain = config["RainforestApi:AmazonDomain"] ?? "amazon.co.uk";
    }

    public async Task<AmazonProductDto> ScrapeAsync(string url)
    {
        var result = new AmazonProductDto { ProductUrl = url };

        var asin = ExtractAsin(url);
        if (string.IsNullOrWhiteSpace(asin))
        {
            result.Error = "Could not extract ASIN from the Amazon URL.";
            return result;
        }

        result.Asin = asin;

        // ── Cache check ───────────────────────────────────────────────────
        var cacheKey = $"asin:{asin}";
        var cached   = await _cache.GetAsync<AmazonProductDto>(cacheKey);
        if (cached != null)
            return cached;

        Console.WriteLine($"[Cache MISS] {asin} — calling Rainforest");

        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            result.Error = "Rainforest API key is not configured.";
            return result;
        }

        var client = _http.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(30);

        // Two parallel calls:
        //   product — full product data + availability + specifications
        //   offers  — ALL used conditions in one call (like_new, very_good, good, acceptable)
        var productTask = FetchJsonAsync(client,
            $"{_baseUrl}?api_key={_apiKey}&type=product&asin={asin}&amazon_domain={_amazonDomain}");

        var offersTask = FetchJsonAsync(client,
            $"{_baseUrl}?api_key={_apiKey}&type=offers&asin={asin}&amazon_domain={_amazonDomain}&condition_filter=used");

        await Task.WhenAll(productTask, offersTask);

        var (productJson, productQuotaHit) = await productTask;
        var (offersJson,  offersQuotaHit)  = await offersTask;

        if (productQuotaHit)
        {
            result.Error     = "Rainforest API credits exhausted — please top up your account at rainforestapi.com";
            result.ScrapedOk = false;
            return result;
        }

        // ── Parse product ─────────────────────────────────────────────────
        try
        {
            var parsed = JsonSerializer.Deserialize<RainforestProductResponse>(productJson, JsonOpts);
            var p      = parsed?.Product;

            if (p != null)
            {
                result.Title        = p.Title ?? "";
                result.Brand        = p.Brand ?? "";
                result.Description  = p.Description ?? "";
                result.BulletPoints = p.FeatureBullets ?? [];
                result.Category     = p.Categories?.FirstOrDefault()?.Name ?? "";

                // ── Availability ──────────────────────────────────────────
                var availRaw = p.Availability?.Raw ?? "";
                Console.WriteLine($"[Rainforest/availability] raw=\"{availRaw}\" type=\"{p.Availability?.Type}\"");

                result.CurrentlyUnavailable =
                    availRaw.Contains("unavailable", StringComparison.OrdinalIgnoreCase) ||
                    availRaw.Contains("out of stock", StringComparison.OrdinalIgnoreCase) ||
                    p.Availability?.Type?.Contains("out_of_stock", StringComparison.OrdinalIgnoreCase) == true;

                // ── Buy Box ───────────────────────────────────────────────
                result.BuyBoxPrice = p.BuyboxWinner?.Price?.Value;
                result.Currency    = p.BuyboxWinner?.Price?.Currency ?? "GBP";
                Console.WriteLine($"[Rainforest/buybox] price={result.BuyBoxPrice} currency={result.Currency} unavailable={result.CurrentlyUnavailable}");

                // ── Images ────────────────────────────────────────────────
                var images = new List<string>();
                if (!string.IsNullOrWhiteSpace(p.MainImage?.Link))
                    images.Add(p.MainImage.Link);
                if (p.Images != null)
                    foreach (var img in p.Images)
                        if (!string.IsNullOrWhiteSpace(img.Link) && !images.Contains(img.Link))
                            images.Add(img.Link);
                result.ImageUrls = images.Take(12).ToList();

                // ── Specifications ────────────────────────────────────────
                var specs = new List<ProductSpec>();
                if (!string.IsNullOrWhiteSpace(result.Brand))
                    specs.Add(new ProductSpec { Name = "Brand", Value = result.Brand });

                if (p.Specifications != null)
                {
                    foreach (var s in p.Specifications)
                    {
                        if (string.IsNullOrWhiteSpace(s.Name) || string.IsNullOrWhiteSpace(s.Value))
                            continue;
                        if (s.Name.Equals("brand", StringComparison.OrdinalIgnoreCase) ||
                            s.Name.Equals("manufacturer", StringComparison.OrdinalIgnoreCase))
                            continue;
                        specs.Add(new ProductSpec { Name = s.Name.Trim(), Value = s.Value.Trim() });
                    }
                }
                result.Specifications = specs;
                Console.WriteLine($"[Rainforest/specs] extracted {specs.Count} specifications");

                result.ScrapedOk = result.Title.Length > 0 || result.ImageUrls.Count > 0;
                if (!result.ScrapedOk)
                    result.Error = "Rainforest returned a product with no usable data.";
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Rainforest/product ERROR] {ex.Message}");
            result.Error = "Failed to parse product data from Rainforest.";
        }

        // ── Parse ALL used offers ─────────────────────────────────────────
        try
        {
            if (!offersQuotaHit)
            {
                var parsed = JsonSerializer.Deserialize<RainforestOffersResponse>(offersJson, JsonOpts);
                var offers = parsed?.Offers ?? [];

                Console.WriteLine($"[Rainforest/offers] total offers returned: {offers.Count}");
                foreach (var o in offers.Take(10))
                    Console.WriteLine($"  condition=\"{o.Condition?.Title}\" price={o.Price?.Value} seller={o.SellerName}");

                if (offers.Count > 0)
                {
                    var firstPriced = offers.FirstOrDefault(o => !string.IsNullOrWhiteSpace(o.Price?.Currency));
                    if (firstPriced != null && string.IsNullOrWhiteSpace(result.Currency))
                        result.Currency = firstPriced.Price!.Currency!;

                    // Like New
                    var likeNew = FilterByCondition(offers, "like new");
                    SetPriceRange(likeNew, out var lnMin, out var lnMax, out var lnCount);
                    result.LikeNewPriceMin = lnMin;
                    result.LikeNewPriceMax = lnMax;
                    result.LikeNewCount    = lnCount;
                    Console.WriteLine($"[Rainforest/offers] Like New: {lnCount} offers, min={lnMin}, max={lnMax}");

                    // Very Good
                    var veryGood = FilterByCondition(offers, "very good");
                    SetPriceRange(veryGood, out var vgMin, out var vgMax, out var vgCount);
                    result.VeryGoodPriceMin = vgMin;
                    result.VeryGoodPriceMax = vgMax;
                    result.VeryGoodCount    = vgCount;
                    Console.WriteLine($"[Rainforest/offers] Very Good: {vgCount} offers, min={vgMin}, max={vgMax}");

                    // Other sellers — top 5 cheapest
                    result.OtherSellers = offers
                        .Where(o => o.Price?.Value != null)
                        .OrderBy(o => o.Price!.Value)
                        .Take(5)
                        .Select(o => new OtherSellerDto
                        {
                            SellerName = o.SellerName ?? "Amazon seller",
                            Condition  = o.Condition?.Title ?? "Used",
                            Price      = o.Price!.Value!.Value,
                            Currency   = o.Price.Currency ?? result.Currency,
                            SellerUrl  = o.Link ?? o.Seller?.Link,
                        })
                        .ToList();
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Rainforest/offers ERROR] {ex.Message}");
        }

        // ── Store in cache if scrape succeeded ────────────────────────────
        if (result.ScrapedOk)
            await _cache.SetAsync(cacheKey, result, TimeSpan.FromHours(24));

        return result;
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static List<RfOffer> FilterByCondition(List<RfOffer> offers, string keyword) =>
        offers
            .Where(o => o.Condition?.Title?.Contains(keyword, StringComparison.OrdinalIgnoreCase) == true
                     && o.Price?.Value != null)
            .ToList();

    private static void SetPriceRange(List<RfOffer> offers,
        out decimal? min, out decimal? max, out int? count)
    {
        var prices = offers.Select(o => o.Price!.Value!.Value).OrderBy(v => v).ToList();
        min   = prices.Count > 0 ? prices.First() : null;
        max   = prices.Count > 0 ? prices.Last()  : null;
        count = prices.Count > 0 ? prices.Count   : null;
    }

    private static async Task<(string json, bool quotaHit)> FetchJsonAsync(HttpClient client, string url)
    {
        Console.WriteLine($"[Rainforest] GET {url[..Math.Min(url.Length, 120)]}…");
        var response = await client.GetAsync(url);
        var body     = await response.Content.ReadAsStringAsync();

        if ((int)response.StatusCode == 402)
        {
            Console.WriteLine("[Rainforest] 402 Payment Required — API credits exhausted");
            return (body, true);
        }

        return (body, false);
    }

    private static string? ExtractAsin(string url)
    {
        var m = Regex.Match(url, @"(?:dp|gp/product)/([A-Z0-9]{10})");
        return m.Success ? m.Groups[1].Value : null;
    }

    private static readonly JsonSerializerOptions JsonOpts =
        new() { PropertyNameCaseInsensitive = true };

    // ── JSON models ───────────────────────────────────────────────────────

    private class RainforestProductResponse { public RfProduct? Product { get; set; } }

    private class RfProduct
    {
        public string? Title       { get; set; }
        public string? Brand       { get; set; }
        public string? Description { get; set; }

        [JsonPropertyName("feature_bullets")]
        public List<string>? FeatureBullets { get; set; }

        [JsonPropertyName("main_image")]
        public RfImage? MainImage { get; set; }

        public List<RfImage>?         Images         { get; set; }
        public List<RfCategory>?      Categories     { get; set; }
        public List<RfSpecification>? Specifications { get; set; }
        public RfAvailability?        Availability   { get; set; }

        [JsonPropertyName("buybox_winner")]
        public RfBuybox? BuyboxWinner { get; set; }
    }

    private class RfAvailability
    {
        public string? Raw  { get; set; }
        public string? Type { get; set; }
    }

    private class RfSpecification { public string? Name { get; set; } public string? Value { get; set; } }
    private class RfBuybox        { public RfPrice? Price { get; set; } }
    private class RfImage         { public string? Link { get; set; } }
    private class RfCategory      { public string? Name { get; set; } }

    private class RainforestOffersResponse { public List<RfOffer>? Offers { get; set; } }

    private class RfOffer
    {
        public RfPrice?     Price     { get; set; }
        public RfCondition? Condition { get; set; }

        [JsonPropertyName("seller_name")]
        public string?   SellerName { get; set; }
        public string?   Link       { get; set; }
        public RfSeller? Seller     { get; set; }
    }

    private class RfSeller
    {
        public string? Name { get; set; }
        public string? Link { get; set; }
    }

    private class RfCondition { public string? Title { get; set; } }

    private class RfPrice
    {
        public decimal? Value    { get; set; }
        public string?  Currency { get; set; }
        public string?  Symbol   { get; set; }
    }
}