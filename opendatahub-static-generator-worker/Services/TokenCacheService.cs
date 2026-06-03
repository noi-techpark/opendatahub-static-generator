using System.Collections.Concurrent;
using System.Text.Json;
using GeneratorWorker.Models;

namespace GeneratorWorker.Services;

/// <summary>Fetches and caches OAuth2 client-credentials tokens, refreshing them before expiry.</summary>
public class TokenCacheService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<TokenCacheService> _logger;

    private record CachedToken(string AccessToken, DateTimeOffset ExpiresAt);
    private readonly ConcurrentDictionary<string, CachedToken> _cache = new();

    public TokenCacheService(IHttpClientFactory httpClientFactory, ILogger<TokenCacheService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<string> GetTokenAsync(string cacheKey, AuthConfig auth, CancellationToken ct = default)
    {
        if (_cache.TryGetValue(cacheKey, out var cached) &&
            cached.ExpiresAt > DateTimeOffset.UtcNow.AddSeconds(30))
            return cached.AccessToken;

        _logger.LogInformation("Fetching new OAuth2 token for '{Key}'", cacheKey);

        var client = _httpClientFactory.CreateClient();

        var body = new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = auth.ClientId!,
            ["client_secret"] = auth.ClientSecret!
        };

        if (!string.IsNullOrWhiteSpace(auth.Scope))
            body["scope"] = auth.Scope;

        var response = await client.PostAsync(auth.TokenUrl, new FormUrlEncodedContent(body), ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var token = root.GetProperty("access_token").GetString()
            ?? throw new InvalidOperationException("Token response missing 'access_token'.");

        var expiresIn = root.TryGetProperty("expires_in", out var exp) ? exp.GetInt32() : 3600;
        _cache[cacheKey] = new CachedToken(token, DateTimeOffset.UtcNow.AddSeconds(expiresIn));

        return token;
    }

    public void Invalidate(string cacheKey) => _cache.TryRemove(cacheKey, out _);
}
