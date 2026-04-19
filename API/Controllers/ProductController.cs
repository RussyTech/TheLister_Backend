using System.Security.Claims;
using API.DTOs;
using API.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

[Authorize]
[ApiController]
[Route("api/products")]
public class ProductController : ControllerBase
{
    private readonly IAmazonProductService _amazon;

    public ProductController(IAmazonProductService amazon)
    {
        _amazon = amazon;
    }

    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    // ─── GET /api/products/search?q=gaming+chair&page=1 ───────────────────────
    [HttpGet("search")]
    public async Task<ActionResult<ProductSearchResult>> Search(
        [FromQuery] string q,
        [FromQuery] int page = 1,
        [FromQuery] string? amazonDomain = null)
    {
        if (string.IsNullOrWhiteSpace(q))
            return BadRequest(new { error = "Search query is required" });

        if (page < 1) page = 1;

        var result = await _amazon.SearchProductsAsync(q, page, amazonDomain);
        return Ok(result);
    }

    // ─── GET /api/products/{asin} ─────────────────────────────────────────────
    [HttpGet("{asin}")]
    public async Task<ActionResult<ProductDto>> GetByAsin(
        string asin,
        [FromQuery] string? amazonDomain = null)
    {
        // Check user's cache first — saves an API call
        var cached = await _amazon.GetCachedProductAsync(asin, UserId);
        if (cached != null) return Ok(cached);

        var product = await _amazon.GetProductByAsinAsync(asin, amazonDomain);
        if (product == null)
            return NotFound(new { error = $"Product {asin} not found on Amazon" });

        return Ok(product);
    }

    // ─── GET /api/products/cached ─────────────────────────────────────────────
    [HttpGet("cached")]
    public async Task<ActionResult<List<ProductDto>>> GetCachedProducts()
    {
        var products = await _amazon.GetUserCachedProductsAsync(UserId);
        return Ok(products);
    }

    // ─── POST /api/products/cache/{asin} ──────────────────────────────────────
    // Saves a product to the user's personal cache (their "saved products" list)
    [HttpPost("cache/{asin}")]
    public async Task<ActionResult<ProductDto>> AddToCache(string asin,[FromQuery] string? amazonDomain = null)
    {
        try
        {
            var product = await _amazon.CacheProductAsync(asin, UserId, amazonDomain);
            return Ok(product);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    // ─── DELETE /api/products/cache/{asin} ────────────────────────────────────
    [HttpDelete("cache/{asin}")]
    public async Task<IActionResult> RemoveFromCache(string asin)
    {
        var removed = await _amazon.RemoveCachedProductAsync(asin, UserId);
        if (!removed)
            return NotFound(new { error = $"Product {asin} not in your cache" });

        return NoContent();
    }
}