using API.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

[ApiController]
[Route("api/ebay/dashboard")]   // ← was [Route("ebay/dashboard")]
[Authorize]
public class EbayDashboardController(IEbayDashboardService _svc) : ControllerBase
{
    private string UserId => User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "";

    [HttpGet("overview")]
    public async Task<IActionResult> GetOverview() =>
        Ok(await _svc.GetOverviewAsync(UserId));

    [HttpGet("sales")]
    public async Task<IActionResult> GetSales([FromQuery] int days = 31) =>
        Ok(await _svc.GetSalesChartAsync(UserId, Math.Clamp(days, 7, 90)));

    [HttpGet("feedback")]
    public async Task<IActionResult> GetFeedback(
    [FromQuery] int limit = 25,
    [FromQuery] int offset = 0) =>
    Ok(await _svc.GetFeedbackAsync(UserId, Math.Clamp(limit, 1, 200), Math.Max(0, offset)));
}