namespace GeneratorWorker.Models;

public class SyncRule
{
    /// <summary>Unique name used as job identifier and in log messages.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Output file name (without path, .json extension added automatically if missing).</summary>
    public string OutputFileName { get; set; } = string.Empty;

    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>Static query string parameters appended to every request.</summary>
    public Dictionary<string, string> QueryStrings { get; set; } = new();

    public AuthConfig Auth { get; set; } = new();

    public PagingConfig Paging { get; set; } = new();

    /// <summary>
    /// Quartz cron expression (6-field, seconds-first).
    /// Examples:
    ///   "0 0 * * * ?"   – every hour
    ///   "0 0 2 * * ?"   – every day at 02:00
    ///   "0 0/30 * * * ?" – every 30 minutes
    /// </summary>
    public string CronExpression { get; set; } = "0 0 * * * ?";

    /// <summary>When true, the job fires once immediately on startup in addition to the cron schedule.</summary>
    public bool RunOnStartup { get; set; } = false;
}
