using API.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace API.Controllers;

[ApiController]
[Route("api/ebay")]
[Authorize]
public class EbayInventoryController : ControllerBase
{
    private readonly IEbayInventoryService _inventoryService;
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    public EbayInventoryController(IEbayInventoryService inventoryService)
    {
        _inventoryService = inventoryService;
    }

    [HttpGet("inventory")]
    public async Task<IActionResult> GetInventory(
        [FromQuery] int limit  = 25,
        [FromQuery] int offset = 0)
    {
        var result = await _inventoryService.GetInventoryAsync(UserId, limit, offset);
        return Ok(result);
    }
}