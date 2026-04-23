using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using API.Data;
using API.Entities.DealFinder;
using API.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace API.Services
{
    public class DealFinderService(
    StoreContext db,
    ICacheService cache,
    ILogger<DealFinderService> log) : IDealFinderService
{
    public async Task<DealFinderPagedResult> GetDealsAsync(DealFinderFilter f, CancellationToken ct = default)
    {
        var cacheKey = $"dealfinder:results:{HashFilter(f)}";
        var cached   = await cache.GetAsync<DealFinderPagedResult>(cacheKey);
        if (cached is not null)
        {
            log.LogDebug("DealFinder results HIT page={Page}", f.Page);
            return cached;
        }
        var q = db.DealFinderDeals.Where(d => d.IsActive).AsNoTracking();
        if (!string.IsNullOrWhiteSpace(f.Keyword))
            q = q.Where(d => d.Title.Contains(f.Keyword) || d.Asin.Contains(f.Keyword));
        if (!string.IsNullOrWhiteSpace(f.Category))
            q = q.Where(d => d.Category == f.Category);
        if (!string.IsNullOrWhiteSpace(f.SellerType))
            q = q.Where(d => d.SellerType == f.SellerType);
        if (f.MinBuyPrice.HasValue)  q = q.Where(d => d.AmazonLikeNewPrice >= f.MinBuyPrice.Value);
        if (f.MaxBuyPrice.HasValue)  q = q.Where(d => d.AmazonLikeNewPrice <= f.MaxBuyPrice.Value);
        if (f.MinSellPrice.HasValue) q = q.Where(d => d.EbayAvgSoldPrice   >= f.MinSellPrice.Value);
        if (f.MaxSellPrice.HasValue) q = q.Where(d => d.EbayAvgSoldPrice   <= f.MaxSellPrice.Value);
        if (f.MinProfit.HasValue) q = q.Where(d => d.Profit >= f.MinProfit.Value);
        if (f.MaxProfit.HasValue) q = q.Where(d => d.Profit <= f.MaxProfit.Value);
        if (f.MinRoi.HasValue)    q = q.Where(d => d.Roi    >= f.MinRoi.Value);
        if (f.MaxRoi.HasValue)    q = q.Where(d => d.Roi    <= f.MaxRoi.Value);
        if (f.MinPriceVariation.HasValue)
            q = q.Where(d => d.PriceVariation90To30 >= f.MinPriceVariation.Value);
        if (f.MinPriceVariationPct.HasValue)
            q = q.Where(d => d.PriceVariationPct90To30 >= f.MinPriceVariationPct.Value);
        if (f.MinSalesRankDrops30.HasValue)
            q = q.Where(d => d.SalesRankDrops30 >= f.MinSalesRankDrops30.Value);
        if (f.MaxSalesRank.HasValue)
            q = q.Where(d => d.SalesRank > 0 && d.SalesRank <= f.MaxSalesRank.Value);
        if (f.MinSellerCount.HasValue) q = q.Where(d => d.SellerCount >= f.MinSellerCount.Value);
        if (f.MaxSellerCount.HasValue) q = q.Where(d => d.SellerCount <= f.MaxSellerCount.Value);
        if (f.MinRating.HasValue)      q = q.Where(d => d.Rating      >= f.MinRating.Value);
        if (f.MinReviewCount.HasValue) q = q.Where(d => d.ReviewCount >= f.MinReviewCount.Value);
        if (f.DiscoveredAfter.HasValue)
            q = q.Where(d => d.DiscoveredAt >= f.DiscoveredAfter.Value);
        q = f.SortBy switch
        {
            "Profit"           => f.SortDesc ? q.OrderByDescending(d => d.Profit)           : q.OrderBy(d => d.Profit),
            "Roi"              => f.SortDesc ? q.OrderByDescending(d => d.Roi)               : q.OrderBy(d => d.Roi),
            "SalesRankDrops30" => f.SortDesc ? q.OrderByDescending(d => d.SalesRankDrops30) : q.OrderBy(d => d.SalesRankDrops30),
            "SalesRank"        => f.SortDesc ? q.OrderByDescending(d => d.SalesRank)        : q.OrderBy(d => d.SalesRank),
            "AmazonLikeNewPrice" => f.SortDesc ? q.OrderByDescending(d => d.AmazonLikeNewPrice) : q.OrderBy(d => d.AmazonLikeNewPrice),
            _                  => f.SortDesc ? q.OrderByDescending(d => d.DiscoveredAt)     : q.OrderBy(d => d.DiscoveredAt),
        };
        var total = await q.CountAsync(ct);
        var items = await q
            .Skip((f.Page - 1) * f.PageSize)
            .Take(f.PageSize)
            .Select(d => new DealFinderDealDto
            {
                Id                      = d.Id,
                Asin                    = d.Asin,
                Title                   = d.Title,
                ImageUrl                = d.ImageUrl,
                Category                = d.Category,
                Brand                   = d.Brand,
                AmazonLikeNewPrice      = d.AmazonLikeNewPrice,
                AmazonNewPrice          = d.AmazonNewPrice,
                EbayAvgSoldPrice        = d.EbayAvgSoldPrice,
                Profit                  = d.Profit,
                Roi                     = d.Roi,
                SalesRank               = d.SalesRank,
                SalesRankDrops30        = d.SalesRankDrops30,
                BoughtLastMonth         = d.BoughtLastMonth,
                Rating                  = d.Rating,
                ReviewCount             = d.ReviewCount,
                SellerType              = d.SellerType,
                SellerCount             = d.SellerCount,
                PriceVariation90To30    = d.PriceVariation90To30,
                PriceVariationPct90To30 = d.PriceVariationPct90To30,
                AmazonUrl               = d.AmazonUrl,
                DiscoveredAt            = d.DiscoveredAt,
                LastUpdated             = d.LastUpdated,
            })
            .ToListAsync(ct);
        var result = new DealFinderPagedResult
        {
            Total = total,
            Page  = f.Page,
            Pages = (int)Math.Ceiling(total / (double)f.PageSize),
            Items = items,
        };
        await cache.SetAsync(cacheKey, result, TimeSpan.FromMinutes(5));
        return result;
    }
    public async Task<List<string>> GetCategoriesAsync(CancellationToken ct = default)
    {
        const string key = "dealfinder:categories";
        var cached = await cache.GetAsync<List<string>>(key);
        if (cached is not null) return cached;
        var cats = await db.DealFinderDeals
            .Where(d => d.IsActive && d.Category != null)
            .Select(d => d.Category!)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync(ct);
        await cache.SetAsync(key, cats, TimeSpan.FromMinutes(30));
        return cats;
    }
    public async Task<DealScanStatus> GetScanStatusAsync(CancellationToken ct = default)
    {
        var total  = await db.DealFinderDeals.CountAsync(ct);
        var active = await db.DealFinderDeals.CountAsync(d => d.IsActive, ct);
        var last   = await db.DealFinderDeals.MaxAsync(d => (DateTime?)d.LastUpdated, ct);
        return new DealScanStatus(last, total, active, DealScannerService.IsScanning);
    }
    private static string HashFilter(DealFinderFilter f)
    {
        var json = JsonSerializer.Serialize(f);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(hash)[..16];
    }
}
}