using API.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

[ApiController]
[Route("api/cache")]
[Authorize]
public class CacheController : ControllerBase
{
    private readonly ICacheService _cache;

    public CacheController(ICacheService cache) => _cache = cache;

    // GET /api/cache/ping  — tells you if Redis is alive
    [HttpGet("ping")]
    public async Task<IActionResult> Ping()
    {
        var ok = await _cache.PingAsync();
        return ok
            ? Ok(new { status = "connected", timestamp = DateTime.UtcNow })
            : StatusCode(503, new { status = "unavailable" });
    }

    // DELETE /api/cache/asin/{asin}  — force refresh a specific product
    [HttpDelete("asin/{asin}")]
    public async Task<IActionResult> ClearAsin(string asin)
    {
        await _cache.RemoveAsync($"asin:{asin}");
        return Ok(new { cleared = $"asin:{asin}" });
    }

    // DELETE /api/cache/all-asins  — wipe all product cache (e.g. after Rainforest key change)
    [HttpDelete("all-asins")]
    public async Task<IActionResult> ClearAllAsins()
    {
        await _cache.RemoveByPrefixAsync("asin:");
        return Ok(new { cleared = "all asin entries" });
    }

    // DELETE /api/cache/ebay-search  — wipe eBay search cache
    [HttpDelete("ebay-search")]
    public async Task<IActionResult> ClearEbaySearch()
    {
        await _cache.RemoveByPrefixAsync("ebay:search:");
        return Ok(new { cleared = "all ebay search entries" });
    }

    // DELETE /api/cache/ebay-categories
    [HttpDelete("ebay-categories")]
    public async Task<IActionResult> ClearEbayCategories()
    {
        await _cache.RemoveByPrefixAsync("ebay:cat:");
        return Ok(new { cleared = "all ebay category entries" });
    }
}