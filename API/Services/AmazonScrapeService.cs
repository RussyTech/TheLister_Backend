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
    private readonly string _apiKey;
    private readonly string _baseUrl;
    private readonly string _amazonDomain;

    public AmazonScrapeService(IHttpClientFactory http, IConfiguration config)
    {
        _http = http;
        _apiKey = config["RainforestApi:ApiKey"] ?? "";
        _baseUrl = config["RainforestApi:BaseUrl"] ?? "https://api.rainforestapi.com/request";
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

        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            result.Error = "Rainforest API key is not configured.";
            return result;
        }

        var client = _http.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(30);

        // Fire both calls in parallel — product details + Like New offers
        var productTask = FetchJsonAsync(client,
            $"{_baseUrl}?api_key={_apiKey}&type=product&asin={asin}&amazon_domain={_amazonDomain}");

        var offersTask = FetchJsonAsync(client,
            $"{_baseUrl}?api_key={_apiKey}&type=offers&asin={asin}&amazon_domain={_amazonDomain}&condition_filter=used_like_new");

        await Task.WhenAll(productTask, offersTask);

        // ── Parse product ────────────────────────────────
        try
        {
            var productJson = await productTask;
            var parsed = JsonSerializer.Deserialize<RainforestProductResponse>(productJson,
                JsonOpts);
            var p = parsed?.Product;
            if (p != null)
            {
                result.Title = p.Title ?? "";
                result.Brand = p.Brand ?? "";
                result.Description = p.Description ?? "";
                result.BulletPoints = p.FeatureBullets ?? [];
                result.Category = p.Categories?.FirstOrDefault()?.Name ?? "";

                // Buy Box price
                result.BuyBoxPrice = p.BuyboxWinner?.Price?.Value;
                result.Currency = p.BuyboxWinner?.Price?.Currency ?? "GBP";

                // Images
                var images = new List<string>();
                if (!string.IsNullOrWhiteSpace(p.MainImage?.Link))
                    images.Add(p.MainImage.Link);
                if (p.Images != null)
                    foreach (var img in p.Images)
                        if (!string.IsNullOrWhiteSpace(img.Link) && !images.Contains(img.Link))
                            images.Add(img.Link);
                result.ImageUrls = images.Take(12).ToList();

                result.ScrapedOk = result.Title.Length > 0 || result.ImageUrls.Count > 0;
                if (!result.ScrapedOk)
                    result.Error = "Rainforest returned a product with no usable data.";
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Rainforest/product] {ex.Message}");
            result.Error = "Failed to parse product data from Rainforest.";
        }

        // ── Parse Like New offers ────────────────────────
        try
        {
            var offersJson = await offersTask;
            var parsed = JsonSerializer.Deserialize<RainforestOffersResponse>(offersJson,
                JsonOpts);
            var offers = parsed?.Offers;
            if (offers != null && offers.Count > 0)
            {
                var prices = offers
                    .Select(o => o.Price?.Value)
                    .Where(v => v != null)
                    .Select(v => v!.Value)
                    .OrderBy(v => v)
                    .ToList();

                if (prices.Count > 0)
                {
                    result.LikeNewPriceMin = prices.First();
                    result.LikeNewPriceMax = prices.Last();
                    result.LikeNewCount = prices.Count;
                    // Use currency from offer if not already set
                    if (string.IsNullOrWhiteSpace(result.Currency))
                        result.Currency = offers.FirstOrDefault()?.Price?.Currency ?? "GBP";
                }
            }
        }
        catch (Exception ex)
        {
            // Non-fatal — product data still shows fine without offer prices
            Console.WriteLine($"[Rainforest/offers] {ex.Message}");
        }

        return result;
    }

    private static async Task<string> FetchJsonAsync(HttpClient client, string url)
    {
        Console.WriteLine($"[Rainforest] GET {url[..Math.Min(url.Length, 120)]}…");
        var response = await client.GetAsync(url);
        return await response.Content.ReadAsStringAsync();
    }

    private static string? ExtractAsin(string url)
    {
        var m = Regex.Match(url, @"(?:dp|gp/product)/([A-Z0-9]{10})");
        return m.Success ? m.Groups[1].Value : null;
    }

    private static readonly JsonSerializerOptions JsonOpts =
        new() { PropertyNameCaseInsensitive = true };

    // ── JSON models ───────────────────────────────────────────────────────

    private class RainforestProductResponse
    {
        public RfProduct? Product { get; set; }
    }

    private class RfProduct
    {
        public string? Title { get; set; }
        public string? Brand { get; set; }
        public string? Description { get; set; }
        [JsonPropertyName("feature_bullets")]
        public List<string>? FeatureBullets { get; set; }
        [JsonPropertyName("main_image")]
        public RfImage? MainImage { get; set; }
        public List<RfImage>? Images { get; set; }
        public List<RfCategory>? Categories { get; set; }
        [JsonPropertyName("buybox_winner")]
        public RfBuybox? BuyboxWinner { get; set; }
    }

    private class RfBuybox
    {
        public RfPrice? Price { get; set; }
    }

    private class RfImage { public string? Link { get; set; } }
    private class RfCategory { public string? Name { get; set; } }

    private class RainforestOffersResponse
    {
        public List<RfOffer>? Offers { get; set; }
    }

    private class RfOffer
    {
        public RfPrice? Price { get; set; }
    }

    private class RfPrice
    {
        public decimal? Value { get; set; }
        public string? Currency { get; set; }
        public string? Symbol { get; set; }
    }
}