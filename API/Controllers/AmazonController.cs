using System.Security.Claims;
using API.Services;
using API.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

[ApiController]
[Route("api/amazon")]
[Authorize]
public class AmazonController(
    IAmazonScrapeService _svc,
    IEbaySearchService _ebaySearch) : ControllerBase
{
    [HttpGet("scrape")]
    public async Task<IActionResult> Scrape([FromQuery] string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return BadRequest(new { error = "url is required" });

        var result = await _svc.ScrapeAsync(url);
        return Ok(result);
    }

    [HttpGet("ebay-compare")]
    public async Task<IActionResult> EbayCompare(
        [FromQuery] string title,
        [FromQuery] string? brand = null)
    {
        if (string.IsNullOrWhiteSpace(title))
            return BadRequest(new { error = "title is required" });

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
        var result = await _ebaySearch.CompareAsync(userId, title, brand);
        return Ok(result);
    }
}