namespace GeneratorWorker.Models;

public class SyncConfig
{
    /// <summary>Directory where generated JSON files are written. Relative paths are resolved from the app's working directory.</summary>
    public string OutputPath { get; set; } = "../../data";

    /// <summary>Global rate limit applied to all outgoing API requests, keyed per host.</summary>
    public RateLimitConfig RateLimit { get; set; } = new();

    public List<SyncRule> Rules { get; set; } = new();
}
