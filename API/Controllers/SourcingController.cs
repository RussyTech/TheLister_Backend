using API.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

[ApiController]
[Route("api/sourcing")]
[Authorize]
public class SourcingController(ISourcingService _svc) : ControllerBase
{
    [HttpPost("upload")]
    [RequestSizeLimit(10 * 1024 * 1024)]  // 10 MB max
    public async Task<IActionResult> Upload(IFormFile file)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "No file provided." });

        try
        {
            var result = await _svc.ParseSpreadsheetAsync(file);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Sourcing] Parse error: {ex}");
            return StatusCode(500, new { error = "Failed to parse file. Check format and try again." });
        }
    }
}