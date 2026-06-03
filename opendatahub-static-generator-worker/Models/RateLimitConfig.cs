namespace GeneratorWorker.Models;

public class RateLimitConfig
{
    /// <summary>Maximum number of requests per second sent to the remote API.</summary>
    public int RequestsPerSecond { get; set; } = 10;

    /// <summary>Maximum number of retry attempts when a 429 response is received.</summary>
    public int MaxRetries { get; set; } = 3;
}
