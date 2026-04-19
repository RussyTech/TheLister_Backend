using API.DTOs;

namespace API.Services.Interfaces;

public interface IAmazonProductService
{
    Task<ProductSearchResult> SearchProductsAsync(string query, int page = 1, string? amazonDomain = null);
    Task<ProductDto?>         GetProductByAsinAsync(string asin, string? amazonDomain = null);
    Task<ProductDto?>         GetCachedProductAsync(string asin, string userId);
    Task<List<ProductDto>>    GetUserCachedProductsAsync(string userId);
    Task<ProductDto>          CacheProductAsync(string asin, string userId, string? amazonDomain = null);
    Task<bool>                RemoveCachedProductAsync(string asin, string userId);
}