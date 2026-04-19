using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using API.DTOs;
using API.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

namespace API.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser>   _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IConfiguration                 _config;

    public AuthController(
        UserManager<ApplicationUser>   userManager,
        SignInManager<ApplicationUser> signInManager,
        IConfiguration                 config)
    {
        _userManager   = userManager;
        _signInManager = signInManager;
        _config        = config;
    }

    // ─── POST /api/auth/register ──────────────────────────────────────────────
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register([FromBody] RegisterRequest req)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var existingUser = await _userManager.FindByEmailAsync(req.Email);
        if (existingUser != null)
            return BadRequest(new { error = "An account with this email already exists" });

        var user = new ApplicationUser
        {
            UserName    = req.Email,
            Email       = req.Email,
            DisplayName = req.DisplayName,
            CreatedAt   = DateTime.UtcNow,
            UpdatedAt   = DateTime.UtcNow
        };

        var result = await _userManager.CreateAsync(user, req.Password);
        if (!result.Succeeded)
            return BadRequest(new { errors = result.Errors.Select(e => e.Description) });

        // All self-registered users start on Standard tier
        await _userManager.AddToRoleAsync(user, "Standard");

        var (accessToken, refreshToken) = await GenerateTokensAsync(user);

        return Ok(new AuthResponse
        {
            AccessToken  = accessToken,
            RefreshToken = refreshToken,
            User         = await MapToUserDtoAsync(user)
        });
    }

    // ─── POST /api/auth/login ─────────────────────────────────────────────────
    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest req)
    {
        var user = await _userManager.FindByEmailAsync(req.Email);
        if (user == null)
            return Unauthorized(new { error = "Invalid email or password" });

        var result = await _signInManager.CheckPasswordSignInAsync(user, req.Password, lockoutOnFailure: false);
        if (!result.Succeeded)
            return Unauthorized(new { error = "Invalid email or password" });

        var (accessToken, refreshToken) = await GenerateTokensAsync(user);

        return Ok(new AuthResponse
        {
            AccessToken  = accessToken,
            RefreshToken = refreshToken,
            User         = await MapToUserDtoAsync(user)
        });
    }

    // ─── POST /api/auth/refresh ───────────────────────────────────────────────
    [HttpPost("refresh")]
    public async Task<ActionResult<AuthResponse>> Refresh([FromBody] RefreshRequest req)
    {
        var principal = GetPrincipalFromExpiredToken(req.AccessToken);
        if (principal == null)
            return Unauthorized(new { error = "Invalid access token" });

        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        var user   = await _userManager.FindByIdAsync(userId!);

        if (user == null
            || user.RefreshToken != req.RefreshToken
            || user.RefreshTokenExpiresAt < DateTime.UtcNow)
            return Unauthorized(new { error = "Invalid or expired refresh token" });

        var (accessToken, refreshToken) = await GenerateTokensAsync(user);

        return Ok(new AuthResponse
        {
            AccessToken  = accessToken,
            RefreshToken = refreshToken,
            User         = await MapToUserDtoAsync(user)
        });
    }

    // ─── POST /api/auth/logout ────────────────────────────────────────────────
    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var user   = await _userManager.FindByIdAsync(userId!);

        if (user != null)
        {
            user.RefreshToken          = null;
            user.RefreshTokenExpiresAt = null;
            user.UpdatedAt             = DateTime.UtcNow;
            await _userManager.UpdateAsync(user);
        }

        return NoContent();
    }

    // ─── GET /api/auth/me ─────────────────────────────────────────────────────
    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<UserDto>> Me()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var user   = await _userManager.FindByIdAsync(userId!);
        if (user == null) return NotFound();
        return Ok(await MapToUserDtoAsync(user));
    }

    // ─── Private Helpers ──────────────────────────────────────────────────────

    private async Task<(string accessToken, string refreshToken)> GenerateTokensAsync(ApplicationUser user)
    {
        var accessToken  = await GenerateJwtAsync(user);
        var refreshToken = GenerateRefreshToken();

        user.RefreshToken          = refreshToken;
        user.RefreshTokenExpiresAt = DateTime.UtcNow.AddDays(30);
        user.UpdatedAt             = DateTime.UtcNow;

        await _userManager.UpdateAsync(user);

        return (accessToken, refreshToken);
    }

    private async Task<string> GenerateJwtAsync(ApplicationUser user)
    {
        var jwtKey = _config["JwtSettings:TokenKey"]
            ?? throw new InvalidOperationException("JWT TokenKey is not configured");

        var key   = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Email,          user.Email!),
            new("displayName",             user.DisplayName ?? string.Empty)
        };

        // Embed roles into the token so [Authorize(Roles = "...")] works without a DB hit
        var roles = await _userManager.GetRolesAsync(user);
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

        var token = new JwtSecurityToken(
            issuer:             "syncpilot-api",
            audience:           "syncpilot-client",
            claims:             claims,
            expires:            DateTime.UtcNow.AddHours(1),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string GenerateRefreshToken()
    {
        var bytes = new byte[64];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes);
    }

    private ClaimsPrincipal? GetPrincipalFromExpiredToken(string token)
    {
        var jwtKey = _config["JwtSettings:TokenKey"]!;

        var parameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateIssuer           = true,
            ValidIssuer              = "syncpilot-api",
            ValidateAudience         = true,
            ValidAudience            = "syncpilot-client",
            ValidateLifetime         = false  // expired tokens are valid for refresh
        };

        try
        {
            var handler   = new JwtSecurityTokenHandler();
            var principal = handler.ValidateToken(token, parameters, out var secToken);

            if (secToken is not JwtSecurityToken jwt
                || !jwt.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.OrdinalIgnoreCase))
                return null;

            return principal;
        }
        catch
        {
            return null;
        }
    }

    private async Task<UserDto> MapToUserDtoAsync(ApplicationUser user)
    {
        var roles = await _userManager.GetRolesAsync(user);

        return new UserDto
        {
            Id             = user.Id,
            Email          = user.Email!,
            DisplayName    = user.DisplayName,
            Role           = roles.FirstOrDefault() ?? "Standard",
            EbayConnected  = false,   // populated by EbayAuthController
            AmazonConnected = false,  // populated by AmazonAuthController
            CreatedAt      = user.CreatedAt
        };
    }
}