using API.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

[ApiController]
[Route("api/amazon")]
[Authorize]
public class AmazonController(IAmazonScrapeService _svc) : ControllerBase
{
    [HttpGet("scrape")]
    public async Task<IActionResult> Scrape([FromQuery] string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return BadRequest(new { error = "url is required" });

        var result = await _svc.ScrapeAsync(url);
        return Ok(result);
    }
}