using System.Collections.Concurrent;
using System.Threading.RateLimiting;
using GeneratorWorker.Models;

namespace GeneratorWorker.Services;

/// <summary>
/// Singleton that holds one TokenBucketRateLimiter per API host so that all rules
/// targeting the same host share the same request budget.
/// </summary>
public sealed class RateLimiterService : IDisposable
{
    private readonly ConcurrentDictionary<string, RateLimiter> _limiters = new();

    public RateLimiter GetOrCreate(string baseUrl, RateLimitConfig config)
    {
        var host = new Uri(baseUrl).Host;

        return _limiters.GetOrAdd(host, _ => new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions
        {
            TokenLimit = config.RequestsPerSecond,
            ReplenishmentPeriod = TimeSpan.FromSeconds(1),
            TokensPerPeriod = config.RequestsPerSecond,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 10_000,
            AutoReplenishment = true
        }));
    }

    public void Dispose()
    {
        foreach (var limiter in _limiters.Values)
            limiter.Dispose();
    }
}
