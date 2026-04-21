using System.Text.Json;
using API.Services.Interfaces;
using Microsoft.Extensions.Caching.Distributed;
using StackExchange.Redis;

namespace API.Services;

public class CacheService : ICacheService
{
    private readonly IDistributedCache _cache;
    private readonly IConnectionMultiplexer? _redis;
    private readonly ILogger<CacheService> _logger;

    private static readonly JsonSerializerOptions JsonOpts =
        new() { PropertyNameCaseInsensitive = true };

    public CacheService(IDistributedCache cache, ILogger<CacheService> logger,
        IConnectionMultiplexer? redis = null)
    {
        _cache  = cache;
        _logger = logger;
        _redis  = redis;
    }

    public async Task<T?> GetAsync<T>(string key)
    {
        try
        {
            var json = await _cache.GetStringAsync(key);
            if (json == null)
            {
                _logger.LogInformation("[Cache MISS] {Key}", key);
                return default;
            }
            _logger.LogInformation("[Cache HIT] {Key}", key);
            return JsonSerializer.Deserialize<T>(json, JsonOpts);
        }
        catch (Exception ex)
        {
            // Error — Redis may not be ready yet or connection dropped
            _logger.LogError("[Cache GET ERROR] {Key}: {ExType} — {Message}", key, ex.GetType().Name, ex.Message);
            return default;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan ttl)
    {
        try
        {
            var json = JsonSerializer.Serialize(value, JsonOpts);
            await _cache.SetStringAsync(key, json,
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = ttl });
            _logger.LogInformation("[Cache SET] {Key} TTL={TTL}", key, ttl);
        }
        catch (Exception ex)
        {
            _logger.LogError("[Cache SET ERROR] {Key}: {ExType} — {Message}", key, ex.GetType().Name, ex.Message);
        }
    }

    public async Task RemoveAsync(string key)
    {
        try
        {
            await _cache.RemoveAsync(key);
            _logger.LogInformation("[Cache REMOVE] {Key}", key);
        }
        catch (Exception ex)
        {
            _logger.LogError("[Cache REMOVE ERROR] {Key}: {ExType} — {Message}", key, ex.GetType().Name, ex.Message);
        }
    }

    public async Task RemoveByPrefixAsync(string prefix)
    {
        if (_redis == null)
        {
            _logger.LogWarning("[Cache] RemoveByPrefix requires IConnectionMultiplexer — not available");
            return;
        }

        try
        {
            var db     = _redis.GetDatabase();
            var server = _redis.GetServer(_redis.GetEndPoints().First());
            var keys   = server.Keys(pattern: $"syncpilot:{prefix}*").ToArray();

            foreach (var key in keys)
                await db.KeyDeleteAsync(key);

            _logger.LogInformation("[Cache CLEAR] Removed {Count} keys with prefix '{Prefix}'",
                keys.Length, prefix);
        }
        catch (Exception ex)
        {
            _logger.LogError("[Cache CLEAR ERROR] Prefix '{Prefix}': {ExType} — {Message}",
                prefix, ex.GetType().Name, ex.Message);
        }
    }

    public async Task<bool> PingAsync()
    {
        try
        {
            await _cache.GetStringAsync("__ping__");
            return true;
        }
        catch
        {
            return false;
        }
    }
}