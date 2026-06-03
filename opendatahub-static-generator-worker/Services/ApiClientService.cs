using System.Net.Http.Headers;
using System.Text;
using GeneratorWorker.Models;
using Microsoft.Extensions.Options;

namespace GeneratorWorker.Services;

/// <summary>Builds and executes authenticated, rate-limited HTTP GET requests.</summary>
public class ApiClientService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly TokenCacheService _tokenCache;
    private readonly RateLimiterService _rateLimiter;
    private readonly RateLimitConfig _rateLimitConfig;
    private readonly ILogger<ApiClientService> _logger;

    public ApiClientService(
        IHttpClientFactory httpClientFactory,
        TokenCacheService tokenCache,
        RateLimiterService rateLimiter,
        IOptions<SyncConfig> config,
        ILogger<ApiClientService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _tokenCache = tokenCache;
        _rateLimiter = rateLimiter;
        _rateLimitConfig = config.Value.RateLimit;
        _logger = logger;
    }

    public async Task<string> GetAsync(
        SyncRule rule,
        Dictionary<string, string> extraParams,
        CancellationToken ct = default)
    {
        var allParams = new Dictionary<string, string>(rule.QueryStrings);
        foreach (var (k, v) in extraParams)
            allParams[k] = v;

        var url = BuildUrl(rule.BaseUrl, allParams);
        var limiter = _rateLimiter.GetOrCreate(rule.BaseUrl, _rateLimitConfig);
        var client = _httpClientFactory.CreateClient("sync");

        for (var attempt = 0; attempt <= _rateLimitConfig.MaxRetries; attempt++)
        {
            // Block here until a token is available — keeps us under the rate limit
            using var lease = await limiter.AcquireAsync(permitCount: 1, ct);
            if (!lease.IsAcquired)
                throw new InvalidOperationException("Rate limiter rejected the request (queue full).");

            var request = new HttpRequestMessage(HttpMethod.Get, url);
            await ApplyAuthAsync(request, rule, ct);

            _logger.LogDebug("GET {Url}", url);

            var response = await client.SendAsync(request, ct);

            if ((int)response.StatusCode == 429)
            {
                if (attempt == _rateLimitConfig.MaxRetries)
                {
                    _logger.LogError("429 Too Many Requests for {Url} — max retries reached.", url);
                    response.EnsureSuccessStatusCode(); // throws
                }

                var delay = ResolveRetryDelay(response, attempt);
                _logger.LogWarning(
                    "429 Too Many Requests for {Url} — waiting {Seconds}s before retry {Attempt}/{Max}.",
                    url, delay.TotalSeconds, attempt + 1, _rateLimitConfig.MaxRetries);

                await Task.Delay(delay, ct);
                continue;
            }

            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync(ct);
        }

        throw new InvalidOperationException("Unreachable.");
    }

    private static string BuildUrl(string baseUrl, Dictionary<string, string> queryParams)
    {
        if (queryParams.Count == 0) return baseUrl;

        var qs = string.Join("&", queryParams.Select(kvp =>
            $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value)}"));

        return baseUrl.Contains('?') ? $"{baseUrl}&{qs}" : $"{baseUrl}?{qs}";
    }

    private async Task ApplyAuthAsync(HttpRequestMessage request, SyncRule rule, CancellationToken ct)
    {
        switch (rule.Auth.Type)
        {
            case AuthType.Basic:
                var credentials = Convert.ToBase64String(
                    Encoding.UTF8.GetBytes($"{rule.Auth.Username}:{rule.Auth.Password}"));
                request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
                break;

            case AuthType.ClientCredentials:
                var token = await _tokenCache.GetTokenAsync(rule.Name, rule.Auth, ct);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                break;
        }
    }

    private static TimeSpan ResolveRetryDelay(HttpResponseMessage response, int attempt)
    {
        // Honour the Retry-After header when present
        if (response.Headers.RetryAfter?.Delta is { } delta && delta > TimeSpan.Zero)
            return delta;

        if (response.Headers.RetryAfter?.Date is { } date)
        {
            var wait = date - DateTimeOffset.UtcNow;
            if (wait > TimeSpan.Zero) return wait;
        }

        // Exponential backoff: 2s, 4s, 8s, …
        return TimeSpan.FromSeconds(Math.Pow(2, attempt + 1));
    }
}
