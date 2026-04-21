using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using API.Services.Interfaces;

namespace API.Services;

public interface IEbayCategoryService
{
    Task<string?> SuggestCategoryIdAsync(string userId, string title);
}

public class EbayCategoryService : IEbayCategoryService
{
    private readonly IHttpClientFactory _http;
    private readonly IEbayAuthService   _ebayAuth;
    private readonly IConfiguration     _config;
    private readonly ICacheService      _cache;

    private bool   IsSandbox      => _config["EbaySettings:Environment"] == "sandbox";
    private string ApiBase        => IsSandbox ? "https://api.sandbox.ebay.com" : "https://api.ebay.com";
    private string MarketId       => _config["EbaySettings:MarketplaceId"] ?? "EBAY_GB";
    private string CategoryTreeId => MarketId switch
    {
        "EBAY_GB" => "3",
        "EBAY_AU" => "15",
        "EBAY_CA" => "2",
        _         => "0",
    };

    private static readonly JsonSerializerOptions JsonOpts =
        new() { PropertyNameCaseInsensitive = true };

    public EbayCategoryService(
        IHttpClientFactory http,
        IEbayAuthService ebayAuth,
        IConfiguration config,
        ICacheService cache)
    {
        _http     = http;
        _ebayAuth = ebayAuth;
        _config   = config;
        _cache    = cache;
    }

    public async Task<string?> SuggestCategoryIdAsync(string userId, string title)
    {
        var trimmed  = title.Length > 80 ? title[..80] : title;
        var cacheKey = $"ebay:cat:{trimmed.ToLowerInvariant()}";

        // ── Cache check (7-day TTL — categories barely change) ────────────
        var cached = await _cache.GetAsync<string>(cacheKey);
        if (cached != null)
        {
            Console.WriteLine($"[eBay/taxonomy] Cache HIT — category ID={cached}");
            return cached;
        }

        var token = await _ebayAuth.GetValidAccessTokenAsync(userId);
        if (token is null) return null;

        var url = $"{ApiBase}/commerce/taxonomy/v1/category_tree/{CategoryTreeId}/get_category_suggestions" +
                  $"?q={Uri.EscapeDataString(trimmed)}";

        try
        {
            var client = _http.CreateClient();
            client.DefaultRequestVersion = new Version(1, 1);
            client.DefaultVersionPolicy  = HttpVersionPolicy.RequestVersionExact;
            client.Timeout = TimeSpan.FromSeconds(10);

            var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            req.Version       = new Version(1, 1);
            req.VersionPolicy = HttpVersionPolicy.RequestVersionExact;

            var resp = await client.SendAsync(req);
            var json = await resp.Content.ReadAsStringAsync();

            Console.WriteLine($"[eBay/taxonomy] HTTP {(int)resp.StatusCode}: {json[..Math.Min(json.Length, 400)]}");

            if (!resp.IsSuccessStatusCode) return null;

            var parsed = JsonSerializer.Deserialize<CategorySuggestionResponse>(json, JsonOpts);
            var top    = parsed?.CategorySuggestions?.FirstOrDefault();
            var catId  = top?.Category?.CategoryId;

            Console.WriteLine($"[eBay/taxonomy] Suggested: {top?.Category?.CategoryName} (ID={catId})");

            // ── Store in cache ────────────────────────────────────────────
            if (!string.IsNullOrWhiteSpace(catId))
                await _cache.SetAsync(cacheKey, catId, TimeSpan.FromDays(7));

            return catId;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[eBay/taxonomy ERROR] {ex.Message}");
            return null;
        }
    }

    private class CategorySuggestionResponse
    {
        public List<CategorySuggestion>? CategorySuggestions { get; set; }
    }

    private class CategorySuggestion { public EbayCategory? Category { get; set; } }

    private class EbayCategory
    {
        public string? CategoryId   { get; set; }
        public string? CategoryName { get; set; }
    }
}