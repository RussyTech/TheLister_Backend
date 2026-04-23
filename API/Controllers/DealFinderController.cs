using API.Entities.DealFinder;
using API.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;


namespace API.Controllers
{
    [ApiController]
[Route("api/deal-finder")]
[Authorize]
public class DealFinderController(IDealFinderService svc) : ControllerBase
{
    [HttpGet("deals")]
    public async Task<IActionResult> GetDeals([FromQuery] DealFinderFilter filter, CancellationToken ct)
        => Ok(await svc.GetDealsAsync(filter, ct));
    [HttpGet("categories")]
    public async Task<IActionResult> GetCategories(CancellationToken ct)
        => Ok(await svc.GetCategoriesAsync(ct));
    [HttpGet("status")]
    public async Task<IActionResult> GetStatus(CancellationToken ct)
        => Ok(await svc.GetScanStatusAsync(ct));
}
}