using API.Data;
using API.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace API.Controllers;

[ApiController]
[Route("api/ebay")]
[Authorize]
public class EbayAuthController : ControllerBase
{
    private readonly IEbayAuthService _ebayAuthService;
    private readonly IConfiguration _config;
    private readonly StoreContext _context;

    public EbayAuthController(
        IEbayAuthService ebayAuthService,
        IConfiguration config,
        StoreContext context)
    {
        _ebayAuthService = ebayAuthService;
        _config          = config;
        _context         = context;
    }

    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    [HttpGet("connect")]
    public IActionResult Connect()
    {
        var url = _ebayAuthService.GetAuthorizationUrl(UserId);
        return Ok(new { url });
    }

    [HttpGet("callback")]
    [AllowAnonymous]
    public async Task<IActionResult> Callback([FromQuery] string code, [FromQuery] string state)
    {
        if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(state))
            return BadRequest(new { error = "Missing code or state" });

        var userId  = Uri.UnescapeDataString(state);
        var success = await _ebayAuthService.ExchangeCodeForTokenAsync(code, userId);

        var frontendUrl = _config["FrontendUrl"] ?? "http://localhost:5173";

        if (!success)
            return Redirect($"{frontendUrl}/dashboard?ebay=error");

        return Redirect($"{frontendUrl}/dashboard?ebay=connected");
    }

    [HttpGet("status")]
    public async Task<IActionResult> Status()
    {
        var token = await _context.EbayTokens
            .FirstOrDefaultAsync(t => t.UserId == UserId);

        if (token is null || token.RefreshTokenExpiresAt < DateTime.UtcNow)
            return Ok(new { connected = false, ebayUsername = (string?)null });

        return Ok(new { connected = true, ebayUsername = token.EbayUsername });
    }

    [HttpDelete("disconnect")]
    public async Task<IActionResult> Disconnect()
    {
        await _ebayAuthService.DisconnectAsync(UserId);
        return Ok(new { message = "eBay account disconnected" });
    }
}