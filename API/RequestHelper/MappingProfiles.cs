using API.DTOs;
using API.Entities;
using AutoMapper;

namespace API.RequestHelper;

public class MappingProfiles : Profile
{
    public MappingProfiles()
    {
        // ─── Auth / User ──────────────────────────────────────────────────────

        // ApplicationUser → UserDto
        // Role is set manually in the controller (requires async UserManager call)
        CreateMap<ApplicationUser, UserDto>()
            .ForMember(d => d.Id,          o => o.MapFrom(s => s.Id))
            .ForMember(d => d.Email,       o => o.MapFrom(s => s.Email ?? string.Empty))
            .ForMember(d => d.DisplayName, o => o.MapFrom(s => s.DisplayName))
            .ForMember(d => d.CreatedAt,   o => o.MapFrom(s => s.CreatedAt))
            .ForMember(d => d.Role,        o => o.Ignore()); // populated in controller via UserManager

        // ─── Product ──────────────────────────────────────────────────────────

        // ProductImage → ProductImageDto
        CreateMap<ProductImage, ProductImageDto>()
            .ForMember(d => d.Url,       o => o.MapFrom(s => s.ImageUrl))
            .ForMember(d => d.SortOrder, o => o.MapFrom(s => s.SortOrder))
            .ForMember(d => d.IsPrimary, o => o.MapFrom(s => s.IsPrimary));

        // ProductCache → ProductDto (images mapped as child collection)
        CreateMap<ProductCache, ProductDto>()
            .ForMember(d => d.Id,            o => o.MapFrom(s => s.Id))
            .ForMember(d => d.Asin,          o => o.MapFrom(s => s.Asin))
            .ForMember(d => d.Title,         o => o.MapFrom(s => s.Title))
            .ForMember(d => d.Description,   o => o.MapFrom(s => s.Description))
            .ForMember(d => d.BulletPoints,  o => o.MapFrom(s => s.BulletPoints))
            .ForMember(d => d.Brand,         o => o.MapFrom(s => s.Brand))
            .ForMember(d => d.AmazonPrice,   o => o.MapFrom(s => s.AmazonPrice))
            .ForMember(d => d.AmazonUrl,     o => o.MapFrom(s => s.AmazonUrl))
            .ForMember(d => d.Category,      o => o.MapFrom(s => s.Category))
            .ForMember(d => d.WeightKg,      o => o.MapFrom(s => s.WeightKg))
            .ForMember(d => d.LastFetchedAt, o => o.MapFrom(s => s.LastFetchedAt))
            .ForMember(d => d.Images,        o => o.MapFrom(s => s.Images.OrderBy(i => i.SortOrder)));

        // ─── eBay Listings ────────────────────────────────────────────────────

        // CreateListingRequest → EbayListing (for saving a new draft)
        CreateMap<CreateListingRequest, EbayListing>()
            .ForMember(d => d.Title,           o => o.MapFrom(s => s.Title.Trim()))
            .ForMember(d => d.Description,     o => o.MapFrom(s => s.Description))
            .ForMember(d => d.SellingPrice,    o => o.MapFrom(s => s.SellingPrice))
            .ForMember(d => d.Quantity,        o => o.MapFrom(s => s.Quantity))
            .ForMember(d => d.EbayCategoryId,  o => o.MapFrom(s => s.EbayCategoryId))
            .ForMember(d => d.ImageUrls,       o => o.MapFrom(s => s.ImageUrls))
            .ForMember(d => d.ItemSpecifics,   o => o.MapFrom(s => s.ItemSpecifics))
            .ForMember(d => d.Condition,       o => o.MapFrom(s => s.Condition))
            .ForMember(d => d.ListingDuration, o => o.MapFrom(s => s.ListingDuration))
            .ForMember(d => d.ProductCacheId,  o => o.MapFrom(s => s.ProductCacheId))
            // These are set by the controller, not the request
            .ForMember(d => d.Id,              o => o.Ignore())
            .ForMember(d => d.UserId,          o => o.Ignore())
            .ForMember(d => d.EbayListingId,   o => o.Ignore())
            .ForMember(d => d.EbayUrl,         o => o.Ignore())
            .ForMember(d => d.Status,          o => o.Ignore())
            .ForMember(d => d.CreatedAt,       o => o.Ignore())
            .ForMember(d => d.UpdatedAt,       o => o.Ignore())
            .ForMember(d => d.EndedAt,         o => o.Ignore())
            .ForMember(d => d.User,            o => o.Ignore())
            .ForMember(d => d.Product,         o => o.Ignore());

        // EbayListing → ListingSummaryDto (for list views — lightweight)
        CreateMap<EbayListing, ListingSummaryDto>()
            .ForMember(d => d.Id,             o => o.MapFrom(s => s.Id))
            .ForMember(d => d.EbayListingId,  o => o.MapFrom(s => s.EbayListingId))
            .ForMember(d => d.Title,          o => o.MapFrom(s => s.Title))
            .ForMember(d => d.SellingPrice,   o => o.MapFrom(s => s.SellingPrice))
            .ForMember(d => d.Quantity,       o => o.MapFrom(s => s.Quantity))
            .ForMember(d => d.Status,         o => o.MapFrom(s => s.Status.ToString()))
            .ForMember(d => d.EbayUrl,        o => o.MapFrom(s => s.EbayUrl))
            .ForMember(d => d.CreatedAt,      o => o.MapFrom(s => s.CreatedAt))
            .ForMember(d => d.UpdatedAt,      o => o.MapFrom(s => s.UpdatedAt))
            // Derive the primary image and ASIN from the linked product if available
            .ForMember(d => d.PrimaryImageUrl, o => o.MapFrom(s =>
                s.ImageUrls != null && s.ImageUrls.Any()
                    ? s.ImageUrls.First()
                    : null))
            .ForMember(d => d.Asin, o => o.MapFrom(s =>
                s.Product != null ? s.Product.Asin : null));

        // EbayListing → ListingDetailDto (for single listing view — full data)
        CreateMap<EbayListing, ListingDetailDto>()
            .ForMember(d => d.Id,              o => o.MapFrom(s => s.Id))
            .ForMember(d => d.EbayListingId,   o => o.MapFrom(s => s.EbayListingId))
            .ForMember(d => d.ProductCacheId,  o => o.MapFrom(s => s.ProductCacheId))
            .ForMember(d => d.Title,           o => o.MapFrom(s => s.Title))
            .ForMember(d => d.Description,     o => o.MapFrom(s => s.Description))
            .ForMember(d => d.SellingPrice,    o => o.MapFrom(s => s.SellingPrice))
            .ForMember(d => d.Quantity,        o => o.MapFrom(s => s.Quantity))
            .ForMember(d => d.EbayCategoryId,  o => o.MapFrom(s => s.EbayCategoryId))
            .ForMember(d => d.ImageUrls,       o => o.MapFrom(s => s.ImageUrls ?? new List<string>()))
            .ForMember(d => d.ItemSpecifics,   o => o.MapFrom(s => s.ItemSpecifics ?? new Dictionary<string, string>()))
            .ForMember(d => d.Condition,       o => o.MapFrom(s => s.Condition))
            .ForMember(d => d.ListingDuration, o => o.MapFrom(s => s.ListingDuration))
            .ForMember(d => d.Status,          o => o.MapFrom(s => s.Status.ToString()))
            .ForMember(d => d.EbayUrl,         o => o.MapFrom(s => s.EbayUrl))
            .ForMember(d => d.CreatedAt,       o => o.MapFrom(s => s.CreatedAt))
            .ForMember(d => d.UpdatedAt,       o => o.MapFrom(s => s.UpdatedAt))
            .ForMember(d => d.EndedAt,         o => o.MapFrom(s => s.EndedAt))
            .ForMember(d => d.Asin,            o => o.MapFrom(s =>
                s.Product != null ? s.Product.Asin : null));

        // ─── Price Comparison ─────────────────────────────────────────────────

        CreateMap<PriceComparison, PriceComparisonDto>()
            .ForMember(d => d.Id,                   o => o.MapFrom(s => s.Id))
            .ForMember(d => d.Asin,                 o => o.MapFrom(s => s.Asin))
            .ForMember(d => d.ProductTitle,         o => o.MapFrom(s => s.ProductTitle))
            .ForMember(d => d.AmazonPrice,          o => o.MapFrom(s => s.AmazonPrice))
            .ForMember(d => d.EbayLowestNewPrice,   o => o.MapFrom(s => s.EbayLowestNewPrice))
            .ForMember(d => d.EbayAverageNewPrice,  o => o.MapFrom(s => s.EbayAverageNewPrice))
            .ForMember(d => d.EstimatedEbayFees,    o => o.MapFrom(s => s.EstimatedEbayFees))
            .ForMember(d => d.EstimatedProfit,      o => o.MapFrom(s => s.EstimatedProfit))
            .ForMember(d => d.ProfitMarginPercent,  o => o.MapFrom(s => s.ProfitMarginPercent))
            .ForMember(d => d.SnapshotAt,           o => o.MapFrom(s => s.SnapshotAt))
            // Computed — opportunity score derived from margin
            .ForMember(d => d.OpportunityScore, o => o.MapFrom(s =>
                s.ProfitMarginPercent >= 25 ? "High" :
                s.ProfitMarginPercent >= 10 ? "Medium" : "Low"));

        // ─── Deal Scans ───────────────────────────────────────────────────────

        CreateMap<DealScan, DealScanDto>()
            .ForMember(d => d.Id,                  o => o.MapFrom(s => s.Id))
            .ForMember(d => d.Asin,                o => o.MapFrom(s => s.Asin))
            .ForMember(d => d.ProductTitle,        o => o.MapFrom(s => s.ProductTitle))
            .ForMember(d => d.ImageUrl,            o => o.MapFrom(s => s.ImageUrl))
            .ForMember(d => d.Category,            o => o.MapFrom(s => s.Category))
            .ForMember(d => d.AmazonPrice,         o => o.MapFrom(s => s.AmazonPrice))
            .ForMember(d => d.EbayAveragePrice,    o => o.MapFrom(s => s.EbayAveragePrice))
            .ForMember(d => d.ProfitMarginPercent, o => o.MapFrom(s => s.ProfitMarginPercent))
            .ForMember(d => d.ScannedAt,           o => o.MapFrom(s => s.ScannedAt))
            // Computed — potential profit in currency
            .ForMember(d => d.PotentialProfit, o => o.MapFrom(s =>
                s.EbayAveragePrice - s.AmazonPrice - (s.EbayAveragePrice * 0.13m)));
    }
}