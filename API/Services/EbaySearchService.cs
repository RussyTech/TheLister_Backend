using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using API.DTOs;
using API.Services.Interfaces;

namespace API.Services;

public interface IEbaySearchService
{
    Task<EbayCompareDto> CompareAsync(string userId, string title, string? brand = null);
}

public class EbaySearchService : IEbaySearchService
{
    private readonly IHttpClientFactory _http;
    private readonly IEbayAuthService   _ebayAuth;
    private readonly IConfiguration     _config;
    private readonly ICacheService      _cache;

    private bool   IsSandbox => _config["EbaySettings:Environment"] == "sandbox";
    private string ApiBase   => IsSandbox ? "https://api.sandbox.ebay.com" : "https://api.ebay.com";
    private string MarketId  => _config["EbaySettings:MarketplaceId"] ?? "EBAY_GB";

    private static readonly JsonSerializerOptions JsonOpts =
        new() { PropertyNameCaseInsensitive = true };

    public EbaySearchService(
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

    public async Task<EbayCompareDto> CompareAsync(string userId, string title, string? brand = null)
    {
        var result     = new EbayCompareDto { SearchTitle = title };
        var searchTerm = BuildSearchTerm(title, brand);
        var cacheKey   = $"ebay:search:{searchTerm.ToLowerInvariant()}";

        // ── Cache check ───────────────────────────────────────────────────
        var cached = await _cache.GetAsync<EbayCompareDto>(cacheKey);
        if (cached != null)
            return cached;

        Console.WriteLine($"[eBay/compare] Cache MISS — searching for: \"{searchTerm}\"");

        var token = await _ebayAuth.GetValidAccessTokenAsync(userId);
        if (token is null)
        {
            Console.WriteLine("[eBay/compare] No valid token — eBay account not connected");
            return result;
        }

        var url = $"{ApiBase}/buy/browse/v1/item_summary/search" +
                  $"?q={Uri.EscapeDataString(searchTerm)}" +
                  $"&sort=price" +
                  $"&limit=20" +
                  $"&filter=buyingOptions%3A%7BFIXED_PRICE%7D";

        try
        {
            var client = _http.CreateClient();
            client.DefaultRequestVersion = new Version(1, 1);
            client.DefaultVersionPolicy  = HttpVersionPolicy.RequestVersionExact;
            client.Timeout = TimeSpan.FromSeconds(20);

            var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            req.Headers.Add("X-EBAY-C-MARKETPLACE-ID", MarketId);
            req.Version       = new Version(1, 1);
            req.VersionPolicy = HttpVersionPolicy.RequestVersionExact;

            var resp = await client.SendAsync(req);
            var json = await resp.Content.ReadAsStringAsync();

            Console.WriteLine($"[eBay/compare] HTTP {(int)resp.StatusCode}: {json[..Math.Min(json.Length, 500)]}");

            if (!resp.IsSuccessStatusCode) return result;

            var parsed = JsonSerializer.Deserialize<BrowseSearchResponse>(json, JsonOpts);
            var items  = parsed?.ItemSummaries ?? [];

            Console.WriteLine($"[eBay/compare] {items.Count} listings returned");

            result.Active = items
                .Select(item => new EbayMarketListingDto
                {
                    Title       = item.Title ?? "",
                    Url         = item.ItemWebUrl ?? "",
                    Image       = item.Image?.ImageUrl,
                    Price       = decimal.TryParse(
                                      item.Price?.Value,
                                      System.Globalization.NumberStyles.Any,
                                      System.Globalization.CultureInfo.InvariantCulture,
                                      out var p) ? p : null,
                    Currency    = item.Price?.Currency ?? "GBP",
                    Condition   = item.Condition,
                    SellerName  = item.Seller?.Username,
                    SellerScore = double.TryParse(item.Seller?.FeedbackPercentage, out var fb) ? fb : null,
                    IsAuction   = false,
                })
                .Take(15)
                .ToList();

            // ── Store in cache ────────────────────────────────────────────
            if (result.Active.Count > 0)
                await _cache.SetAsync(cacheKey, result, TimeSpan.FromHours(1));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[eBay/compare ERROR] {ex.Message}");
        }

        return result;
    }

    private static string BuildSearchTerm(string title, string? brand)
    {
        var separators = new[] { " - ", " | ", " (", " – ", " — " };
        var clean      = title;
        foreach (var sep in separators)
        {
            var idx = clean.IndexOf(sep, StringComparison.Ordinal);
            if (idx > 12) { clean = clean[..idx]; break; }
        }

        if (clean.Length > 60)
        {
            clean = clean[..60];
            var lastSpace = clean.LastIndexOf(' ');
            if (lastSpace > 20) clean = clean[..lastSpace];
        }

        clean = clean.Replace(",", "").Trim();

        if (!string.IsNullOrWhiteSpace(brand))
        {
            var firstBrandWord = brand.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                                      .FirstOrDefault() ?? "";
            if (!string.IsNullOrWhiteSpace(firstBrandWord) &&
                !clean.Contains(firstBrandWord, StringComparison.OrdinalIgnoreCase))
                clean = $"{firstBrandWord} {clean}";
        }

        return clean.Trim();
    }

    private class BrowseSearchResponse
    {
        public int Total { get; set; }

        [JsonPropertyName("itemSummaries")]
        public List<BrowseItem> ItemSummaries { get; set; } = [];
    }

    private class BrowseItem
    {
        public string?       Title      { get; set; }
        public string?       ItemWebUrl { get; set; }
        public string?       Condition  { get; set; }
        public BrowseImage?  Image      { get; set; }
        public BrowsePrice?  Price      { get; set; }
        public BrowseSeller? Seller     { get; set; }
    }

    private class BrowseImage  { public string? ImageUrl { get; set; } }
    private class BrowsePrice  { public string? Value { get; set; } public string? Currency { get; set; } }
    private class BrowseSeller { public string? Username { get; set; } public string? FeedbackPercentage { get; set; } }
}