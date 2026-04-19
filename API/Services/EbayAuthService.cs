using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using API.Data;
using API.Entities;
using API.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace API.Services;

public class EbayAuthService : IEbayAuthService
{
    private readonly IConfiguration _config;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly StoreContext _context;

    private string ClientId     => _config["EbaySettings:ClientId"]!;
    private string ClientSecret => _config["EbaySettings:ClientSecret"]!;
    private string RuName       => _config["EbaySettings:RuName"]!;
    private string RedirectUri  => _config["EbaySettings:RedirectUri"]!;
    private bool   IsSandbox    => _config["EbaySettings:Environment"] == "sandbox";

    private string AuthBaseUrl => IsSandbox
        ? "https://auth.sandbox.ebay.com"
        : "https://auth.ebay.com";

    private string ApiBaseUrl => IsSandbox
        ? "https://api.sandbox.ebay.com"
        : "https://api.ebay.com";

    // Commerce Identity API uses apiz.ebay.com (note the 'z')
    private string IdentityApiBaseUrl => IsSandbox
        ? "https://apiz.sandbox.ebay.com"
        : "https://apiz.ebay.com";

    private static readonly string[] Scopes =
    [
        "https://api.ebay.com/oauth/api_scope",
        "https://api.ebay.com/oauth/api_scope/sell.inventory",
        "https://api.ebay.com/oauth/api_scope/sell.account",
        "https://api.ebay.com/oauth/api_scope/sell.fulfillment",
        "https://api.ebay.com/oauth/api_scope/commerce.identity.readonly"
    ];

    public EbayAuthService(
        IConfiguration config,
        IHttpClientFactory httpClientFactory,
        StoreContext context)
    {
        _config            = config;
        _httpClientFactory = httpClientFactory;
        _context           = context;
    }

    public string GetAuthorizationUrl(string userId)
    {
        var scope = Uri.EscapeDataString(string.Join(" ", Scopes));
        var state = Uri.EscapeDataString(userId);

        return $"{AuthBaseUrl}/oauth2/authorize" +
               $"?client_id={ClientId}" +
               $"&response_type=code" +
               $"&redirect_uri={Uri.EscapeDataString(RuName)}" +
               $"&scope={scope}" +
               $"&state={state}";
    }

    public async Task<bool> ExchangeCodeForTokenAsync(string code, string userId)
    {
        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(30);

        var credentials = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{ClientId}:{ClientSecret}"));

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Basic", credentials);

        var body = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"]   = "authorization_code",
            ["code"]         = code,
            ["redirect_uri"] = RuName
        });

        Console.WriteLine($"[eBay] Exchanging code for token. RedirectUri={RuName}");

        HttpResponseMessage response;
        try
        {
            response = await client.PostAsync($"{ApiBaseUrl}/identity/v1/oauth2/token", body);
        }
        catch (TaskCanceledException)
        {
            Console.WriteLine("[eBay] Token exchange timed out after 30s");
            return false;
        }

        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"[eBay] Token exchange failed {(int)response.StatusCode}: {err}");
            return false;
        }

        Console.WriteLine("[eBay] Token exchange succeeded");

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var accessToken      = root.GetProperty("access_token").GetString()!;
        var refreshToken     = root.GetProperty("refresh_token").GetString()!;
        var expiresIn        = root.GetProperty("expires_in").GetInt32();
        var refreshExpiresIn = root.GetProperty("refresh_token_expires_in").GetInt32();

        var ebayUsername = await FetchEbayUsernameAsync(accessToken);
        Console.WriteLine($"[eBay] Username fetched: {ebayUsername ?? "unknown"}");

        var existing = await _context.EbayTokens
            .FirstOrDefaultAsync(t => t.UserId == userId);

        if (existing is null)
        {
            _context.EbayTokens.Add(new EbayToken
            {
                UserId                = userId,
                AccessToken           = accessToken,
                RefreshToken          = refreshToken,
                AccessTokenExpiresAt  = DateTime.UtcNow.AddSeconds(expiresIn),
                RefreshTokenExpiresAt = DateTime.UtcNow.AddSeconds(refreshExpiresIn),
                EbayUsername          = ebayUsername,
            });
        }
        else
        {
            existing.AccessToken           = accessToken;
            existing.RefreshToken          = refreshToken;
            existing.AccessTokenExpiresAt  = DateTime.UtcNow.AddSeconds(expiresIn);
            existing.RefreshTokenExpiresAt = DateTime.UtcNow.AddSeconds(refreshExpiresIn);
            existing.EbayUsername          = ebayUsername;
            existing.UpdatedAt             = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();
        Console.WriteLine("[eBay] Token saved to database");
        return true;
    }

    private async Task<string?> FetchEbayUsernameAsync(string accessToken)
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", accessToken);

            var resp = await client.GetAsync($"{IdentityApiBaseUrl}/commerce/identity/v1/user/");
            if (!resp.IsSuccessStatusCode)
            {
                var err = await resp.Content.ReadAsStringAsync();
                Console.WriteLine($"[eBay] FetchUsername failed {(int)resp.StatusCode}: {err}");
                return null;
            }

            var json = await resp.Content.ReadAsStringAsync();
            Console.WriteLine($"[eBay] FetchUsername response: {json}");
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty("username", out var u)
                ? u.GetString()
                : null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[eBay] FetchUsername exception: {ex.Message}");
            return null;
        }
    }

    public async Task<string?> GetValidAccessTokenAsync(string userId)
    {
        var token = await _context.EbayTokens
            .FirstOrDefaultAsync(t => t.UserId == userId);

        if (token is null) return null;

        if (token.AccessTokenExpiresAt > DateTime.UtcNow.AddMinutes(5))
            return token.AccessToken;

        if (token.RefreshTokenExpiresAt < DateTime.UtcNow)
        {
            _context.EbayTokens.Remove(token);
            await _context.SaveChangesAsync();
            return null;
        }

        return await RefreshAccessTokenAsync(token);
    }

    private async Task<string?> RefreshAccessTokenAsync(EbayToken token)
    {
        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(30);

        var credentials = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{ClientId}:{ClientSecret}"));

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Basic", credentials);

        var body = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"]    = "refresh_token",
            ["refresh_token"] = token.RefreshToken,
            ["scope"]         = string.Join(" ", Scopes)
        });

        var response = await client.PostAsync($"{ApiBaseUrl}/identity/v1/oauth2/token", body);

        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"[eBay] Refresh failed {(int)response.StatusCode}: {err}");
            return null;
        }

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        token.AccessToken          = root.GetProperty("access_token").GetString()!;
        token.AccessTokenExpiresAt = DateTime.UtcNow.AddSeconds(root.GetProperty("expires_in").GetInt32());
        token.UpdatedAt            = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return token.AccessToken;
    }

    public async Task<bool> DisconnectAsync(string userId)
    {
        var token = await _context.EbayTokens
            .FirstOrDefaultAsync(t => t.UserId == userId);

        if (token is null) return false;

        _context.EbayTokens.Remove(token);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> IsConnectedAsync(string userId)
    {
        return await _context.EbayTokens
            .AnyAsync(t => t.UserId == userId &&
                           t.RefreshTokenExpiresAt > DateTime.UtcNow);
    }
}