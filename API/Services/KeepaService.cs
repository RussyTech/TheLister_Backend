using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using API.Config;
using API.Services.Interfaces;
using Microsoft.Extensions.Options;

namespace API.Services
{
    public class KeepaService(
    HttpClient http,
    ICacheService cache,
    IOptions<KeepaSettings> opts,
    ILogger<KeepaService> log) : IKeepaService
{
    // domain=2 = amazon.co.uk
    private const int UkDomain = 2;
    public async Task<KeepaProductData?> GetProductDataAsync(string asin, CancellationToken ct = default)
    {
        var cacheKey = $"dealfinder:keepa:{asin}";
        var cached   = await cache.GetAsync<KeepaProductData>(cacheKey);
        if (cached is not null)
        {
            log.LogDebug("Keepa HIT {Asin}", asin);
            return cached;
        }
        var key = opts.Value.ApiKey;
        if (string.IsNullOrEmpty(key)) return null;
        try
        {
            var url  = $"https://api.keepa.com/product?key={key}&domain={UkDomain}&asin={asin}&stats=90&history=0";
            var resp = await http.GetStringAsync(url, ct);
            var doc  = JsonDocument.Parse(resp);
            var products = doc.RootElement.GetProperty("products");
            if (products.GetArrayLength() == 0) return null;
            var p     = products[0];
            var stats = p.GetProperty("stats");
            // Keepa prices are in pence (integer × 0.01 = £)
            static decimal ToGbp(JsonElement el, string prop)
            {
                if (!el.TryGetProperty(prop, out var v)) return 0;
                var arr = v.EnumerateArray().ToArray();
                // index 0 = new price
                if (arr.Length == 0) return 0;
                var raw = arr[0].GetInt32();
                return raw < 0 ? 0 : raw / 100m;
            }
            var rank = 0;
            if (p.TryGetProperty("salesRanks", out var sr))
            {
                // salesRanks is a dict; we want the root category rank
                foreach (var cat in sr.EnumerateObject())
                {
                    var arr = cat.Value.EnumerateArray().ToArray();
                    if (arr.Length >= 2)
                    {
                        rank = arr[^1].GetInt32(); // last value = current rank
                        break;
                    }
                }
            }
            var drops30 = stats.TryGetProperty("salesRankDrops30", out var d30) ? d30.GetInt32() : 0;
            var avg90   = ToGbp(stats, "avg90");
            var avg30   = ToGbp(stats, "avg30");
            var current = ToGbp(stats, "current");
            var result = new KeepaProductData(rank, drops30, avg90, avg30, current);
            await cache.SetAsync(cacheKey, result, TimeSpan.FromMinutes(60));
            return result;
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "Keepa error for {Asin}", asin);
            return null;
        }
    }
}
}