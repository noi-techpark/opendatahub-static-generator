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
    /// Optional group name for sequential chaining. Rules sharing the same ChainGroup run
    /// one after another in config order. Only the first rule in the group needs a CronExpression
    /// and RunOnStartup — the rest are triggered automatically when the previous job finishes.
    /// Rules without a ChainGroup are scheduled independently.
    /// </summary>
    public string? ChainGroup { get; set; }

    /// <summary>
    /// Quartz cron expression (6-field, seconds-first). Required for the first rule in a chain
    /// group (or any unchained rule). Ignored for non-first rules in a chain group.
    /// Examples:
    ///   "0 0 * * * ?"   – every hour
    ///   "0 0 2 * * ?"   – every day at 02:00
    ///   "0 0 3,15 * * ?" – every day at 03:00 and 15:00
    /// </summary>
    public string? CronExpression { get; set; }

    /// <summary>When true, the job fires once immediately on startup in addition to the cron schedule.</summary>
    public bool RunOnStartup { get; set; } = false;
}
