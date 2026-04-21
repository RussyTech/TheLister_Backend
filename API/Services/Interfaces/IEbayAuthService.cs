namespace API.Services.Interfaces;

public interface IEbayAuthService
{
    string GetAuthorizationUrl(string userId);
    Task<bool> ExchangeCodeForTokenAsync(string code, string userId);
    Task<string?> GetValidAccessTokenAsync(string userId);
    Task<bool> DisconnectAsync(string userId);
    Task<bool> IsConnectedAsync(string userId);
    Task<string?> GetEbayUsernameAsync(string userId);
}