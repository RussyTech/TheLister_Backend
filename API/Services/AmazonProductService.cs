using System.Text.Json;
using System.Text.Json.Nodes;
using API.Data;
using API.DTOs;
using API.Entities;
using API.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace API.Services;

public class AmazonProductService : IAmazonProductService
{
    private readonly HttpClient     _http;
    private readonly IMemoryCache   _cache;
    private readonly StoreContext   _context;
    private readonly IConfiguration _config;

    private string ApiKey        => _config["RainforestApi:ApiKey"]!;
    private string BaseUrl       => _config["RainforestApi:BaseUrl"]!;
    private string DefaultDomain => _config["RainforestApi:AmazonDomain"] ?? "amazon.com";

    public AmazonProductService(
        HttpClient     http,
        IMemoryCache   cache,
        StoreContext   context,
        IConfiguration config)
    {
        _http    = http;
        _cache   = cache;
        _context = context;
        _config  = config;
    }

    // ─── Search ───────────────────────────────────────────────────────────────
    public async Task<ProductSearchResult> SearchProductsAsync(
        string  query,
        int     page         = 1,
        string? amazonDomain = null)
    {
        var domain   = amazonDomain ?? DefaultDomain;
        var cacheKey = $"search:{domain}:{query}:{page}";

        if (_cache.TryGetValue(cacheKey, out ProductSearchResult? cached))
            return cached!;

        var url = $"{BaseUrl}?api_key={ApiKey}&type=search&amazon_domain={domain}" +
                  $"&search_term={Uri.EscapeDataString(query)}&page={page}";

        var response = await _http.GetStringAsync(url);
        var json     = JsonNode.Parse(response);

        var result = new ProductSearchResult
        {
            Query        = query,
            Page         = page,
            TotalResults = json?["pagination"]?["total_results"]?.GetValue<int>() ?? 0,
            Products     = ParseSearchResults(json)
        };

        _cache.Set(cacheKey, result, TimeSpan.FromMinutes(30));
        return result;
    }

    // ─── Get by ASIN ──────────────────────────────────────────────────────────
    public async Task<ProductDto?> GetProductByAsinAsync(string asin, string? amazonDomain = null)
    {
        var domain   = amazonDomain ?? DefaultDomain;
        var cacheKey = $"product:{domain}:{asin}";

        if (_cache.TryGetValue(cacheKey, out ProductDto? cached))
            return cached;

        var url = $"{BaseUrl}?api_key={ApiKey}&type=product&amazon_domain={domain}&asin={asin}";

        try
        {
            var response = await _http.GetStringAsync(url);
            var json     = JsonNode.Parse(response)?["product"];
            if (json == null) return null;

            var dto = ParseProduct(json);
            _cache.Set(cacheKey, dto, TimeSpan.FromHours(2));
            return dto;
        }
        catch
        {
            return null;
        }
    }

    // ─── Get single cached product ────────────────────────────────────────────
    public async Task<ProductDto?> GetCachedProductAsync(string asin, string userId)
    {
        var entity = await _context.ProductCache
            .Include(p => p.Images)
            .FirstOrDefaultAsync(p => p.Asin == asin && p.UserId == userId);

        return entity == null ? null : MapCacheToDto(entity);
    }

    // ─── List all cached products for a user ──────────────────────────────────
    public async Task<List<ProductDto>> GetUserCachedProductsAsync(string userId)
    {
        var entities = await _context.ProductCache
            .Include(p => p.Images)
            .Where(p => p.UserId == userId)
            .OrderByDescending(p => p.LastFetchedAt)
            .ToListAsync();

        return entities.Select(MapCacheToDto).ToList();
    }

    // ─── Save / refresh a product in the user's cache ─────────────────────────
    public async Task<ProductDto> CacheProductAsync(string asin, string userId, string? amazonDomain = null)
    {
        // Return existing record if refreshed within the last 24 hours
        var existing = await _context.ProductCache
            .Include(p => p.Images)
            .FirstOrDefaultAsync(p => p.Asin == asin && p.UserId == userId);

        if (existing != null && existing.LastFetchedAt > DateTime.UtcNow.AddHours(-24))
            return MapCacheToDto(existing);

        // Fetch fresh data from Rainforest API
        var dto = await GetProductByAsinAsync(asin, amazonDomain)
            ?? throw new InvalidOperationException($"Product {asin} not found on Amazon");

        if (existing == null)
        {
            existing = new ProductCache
            {
                Asin      = asin,
                UserId    = userId,
                CreatedAt = DateTime.UtcNow
            };
            _context.ProductCache.Add(existing);
        }

        existing.Title         = dto.Title;
        existing.Description   = dto.Description;
        existing.BulletPoints  = dto.BulletPoints;
        existing.Brand         = dto.Brand;
        existing.AmazonPrice   = dto.AmazonPrice;
        existing.AmazonUrl     = dto.AmazonUrl;
        existing.Category      = dto.Category;
        existing.LastFetchedAt = DateTime.UtcNow;

        // Rebuild image collection
        if (existing.Id > 0)
        {
            var oldImages = _context.ProductImages.Where(i => i.ProductCacheId == existing.Id);
            _context.ProductImages.RemoveRange(oldImages);
        }

        existing.Images = dto.Images.Select((img, i) => new ProductImage
        {
            ImageUrl  = img.Url,
            IsPrimary = img.IsPrimary,
            SortOrder = img.SortOrder
        }).ToList();

        await _context.SaveChangesAsync();
        return MapCacheToDto(existing);
    }

    // ─── Remove from cache ────────────────────────────────────────────────────
    public async Task<bool> RemoveCachedProductAsync(string asin, string userId)
    {
        var entity = await _context.ProductCache
            .FirstOrDefaultAsync(p => p.Asin == asin && p.UserId == userId);

        if (entity == null) return false;

        _context.ProductCache.Remove(entity);
        await _context.SaveChangesAsync();
        return true;
    }

    // ─── Private: parse Rainforest search response ────────────────────────────
    private static List<ProductDto> ParseSearchResults(JsonNode? json)
    {
        var results = new List<ProductDto>();
        var items   = json?["search_results"]?.AsArray();
        if (items == null) return results;

        foreach (var item in items)
        {
            if (item == null) continue;

            results.Add(new ProductDto
            {
                Asin        = item["asin"]?.GetValue<string>()  ?? string.Empty,
                Title       = item["title"]?.GetValue<string>() ?? string.Empty,
                AmazonPrice = item["price"]?["value"]?.GetValue<decimal>() ?? 0m,
                AmazonUrl   = item["link"]?.GetValue<string>(),
                Images      = item["image"]?.GetValue<string>() is string img
                              ? [new ProductImageDto { Url = img, IsPrimary = true, SortOrder = 0 }]
                              : [],
                Rating      = item["rating"]?.GetValue<decimal>(),
                ReviewCount = item["ratings_total"]?.GetValue<int>(),
                IsPrime     = item["is_prime"]?.GetValue<bool>() ?? false,
                LastFetchedAt = DateTime.UtcNow
            });
        }

        return results;
    }

    // ─── Private: parse Rainforest product detail response ───────────────────
    private static ProductDto ParseProduct(JsonNode json)
    {
        // Images
        var images = new List<ProductImageDto>();
        if (json["main_image"]?["link"]?.GetValue<string>() is string mainImg)
            images.Add(new ProductImageDto { Url = mainImg, IsPrimary = true, SortOrder = 0 });

        var extras = json["images"]?.AsArray();
        if (extras != null)
        {
            foreach (var img in extras)
            {
                if (img?["link"]?.GetValue<string>() is string link &&
                    !images.Any(i => i.Url == link))
                {
                    images.Add(new ProductImageDto
                    {
                        Url       = link,
                        IsPrimary = false,
                        SortOrder = images.Count
                    });
                }
            }
        }

        // Bullet points → serialize as JSON string (matches BulletPoints column)
        var bullets = json["feature_bullets"]?.AsArray()
            ?.Select(b => b?.GetValue<string>() ?? string.Empty)
            .Where(b => !string.IsNullOrWhiteSpace(b))
            .ToList();

        var bulletJson = bullets?.Count > 0
            ? JsonSerializer.Serialize(bullets)
            : null;

        // Category — use the most specific (last) category
        var category = json["categories"]?.AsArray()
            ?.LastOrDefault()?["name"]?.GetValue<string>();

        // Price — prefer buybox price, fall back to listed price
        var price = json["buybox_winner"]?["price"]?["value"]?.GetValue<decimal>()
                 ?? json["price"]?["value"]?.GetValue<decimal>()
                 ?? 0m;

        return new ProductDto
        {
            Asin          = json["asin"]?.GetValue<string>()        ?? string.Empty,
            Title         = json["title"]?.GetValue<string>()       ?? string.Empty,
            Description   = json["description"]?.GetValue<string>(),
            BulletPoints  = bulletJson,
            Brand         = json["brand"]?.GetValue<string>(),
            AmazonPrice   = price,
            AmazonUrl     = json["link"]?.GetValue<string>(),
            Category      = category,
            Images        = images,
            Rating        = json["rating"]?.GetValue<decimal>(),
            ReviewCount   = json["ratings_total"]?.GetValue<int>(),
            IsPrime       = json["is_prime"]?.GetValue<bool>() ?? false,
            LastFetchedAt = DateTime.UtcNow
        };
    }

    // ─── Private: map DB entity → DTO ────────────────────────────────────────
    private static ProductDto MapCacheToDto(ProductCache e) => new()
    {
        Id            = e.Id,
        Asin          = e.Asin,
        Title         = e.Title,
        Description   = e.Description,
        BulletPoints  = e.BulletPoints,
        Brand         = e.Brand,
        AmazonPrice   = e.AmazonPrice,
        AmazonUrl     = e.AmazonUrl,
        Category      = e.Category,
        WeightKg      = e.WeightKg,
        LastFetchedAt = e.LastFetchedAt,
        Images        = e.Images
            .OrderBy(i => i.SortOrder)
            .Select(i => new ProductImageDto
            {
                Url       = i.ImageUrl,
                IsPrimary = i.IsPrimary,
                SortOrder = i.SortOrder
            }).ToList()
    };
}