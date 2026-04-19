using API.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;

namespace API.Controllers;

[ApiController]
[Route("api/ebay/images")]
[Authorize]
public class EbayImageController : ControllerBase
{
    private readonly IEbayAuthService   _ebayAuth;
    private readonly IHttpClientFactory _http;
    private readonly IConfiguration     _config;

    private string UserId      => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    private bool   IsSandbox   => _config["EbaySettings:Environment"] == "sandbox";
    private string ApiBase     => IsSandbox ? "https://api.sandbox.ebay.com" : "https://api.ebay.com";
    private string MarketId    => _config["EbaySettings:MarketplaceId"] ?? "EBAY_GB";

    public EbayImageController(IEbayAuthService ebayAuth, IHttpClientFactory http, IConfiguration config)
    {
        _ebayAuth = ebayAuth;
        _http     = http;
        _config   = config;
    }

    [HttpPost]
    [RequestSizeLimit(12 * 1024 * 1024)]
    public async Task<IActionResult> UploadImage(IFormFile file)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "No file provided" });

        var allowed = new[] { "image/jpeg", "image/png", "image/gif", "image/webp" };
        if (!allowed.Contains(file.ContentType.ToLower()))
            return BadRequest(new { error = "Use JPEG, PNG, GIF or WebP" });

        var token = await _ebayAuth.GetValidAccessTokenAsync(UserId);
        if (token is null) return Unauthorized(new { error = "eBay account not connected" });

        using var ms = new MemoryStream();
        await file.CopyToAsync(ms);
        var bytes = ms.ToArray();

        var client = _http.CreateClient();
        client.DefaultRequestVersion = new Version(1, 1);
        client.DefaultVersionPolicy  = HttpVersionPolicy.RequestVersionExact;
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
        client.DefaultRequestHeaders.TryAddWithoutValidation("X-EBAY-C-MARKETPLACE-ID", MarketId);
        // client.DefaultRequestHeaders.TryAddWithoutValidation("Content-Language", "en-GB");

        var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType);

        var url = $"{ApiBase}/sell/media/v1/image";
        Console.WriteLine($"[eBay Image] POST {url}");
        Console.WriteLine($"[eBay Image] File: {file.FileName} | {file.ContentType} | {file.Length / 1024} KB | Market: {MarketId}");

        var response = await client.PostAsync(url, content);
        var body     = await response.Content.ReadAsStringAsync();

        Console.WriteLine($"[eBay Image] HTTP {(int)response.StatusCode}: {(string.IsNullOrWhiteSpace(body) ? "(empty body)" : body[..Math.Min(500, body.Length)])}");

        if (!response.IsSuccessStatusCode)
            return BadRequest(new { error = $"eBay image upload failed ({(int)response.StatusCode}): {body[..Math.Min(300, body.Length)]}" });

        using var doc = JsonDocument.Parse(body);
        if (!doc.RootElement.TryGetProperty("imageUrl", out var urlProp))
            return BadRequest(new { error = "No imageUrl in eBay response" });

        var imageUrl = urlProp.GetString();
        Console.WriteLine($"[eBay Image] Success: {imageUrl}");
        return Ok(new { imageUrl });
    }
}