using Microsoft.Extensions.Options;
using Quartz;
using GeneratorWorker.Models;
using GeneratorWorker.Services;

namespace GeneratorWorker.Jobs;

[DisallowConcurrentExecution]
public class SyncJob : IJob
{
    private readonly SyncService _syncService;
    private readonly SyncConfig _config;
    private readonly ILogger<SyncJob> _logger;

    public SyncJob(SyncService syncService, IOptions<SyncConfig> config, ILogger<SyncJob> logger)
    {
        _syncService = syncService;
        _config = config.Value;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var ruleName = context.JobDetail.JobDataMap.GetString("RuleName");
        var rule = _config.Rules.FirstOrDefault(r => r.Name == ruleName);

        if (rule is null)
        {
            _logger.LogWarning("No sync rule found with name '{RuleName}'", ruleName);
            return;
        }

        try
        {
            var outputPath = Path.GetFullPath(_config.OutputPath);
            await _syncService.SyncAsync(rule, outputPath, context.CancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Sync failed for rule '{RuleName}'", ruleName);
        }
    }
}
