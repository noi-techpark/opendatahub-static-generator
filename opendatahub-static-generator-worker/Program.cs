using Quartz;
using Quartz.Impl.Matchers;
using Quartz.Listener;
using GeneratorWorker.Jobs;
using GeneratorWorker.Models;
using GeneratorWorker.Services;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<SyncConfig>(builder.Configuration.GetSection("Sync"));
builder.Services.AddHttpClient("sync");
builder.Services.AddSingleton<TokenCacheService>();
builder.Services.AddSingleton<RateLimiterService>();
builder.Services.AddScoped<ApiClientService>();
builder.Services.AddScoped<SyncService>();

var syncConfig = builder.Configuration.GetSection("Sync").Get<SyncConfig>() ?? new SyncConfig();

var chainGroups = syncConfig.Rules
    .Where(r => !string.IsNullOrEmpty(r.ChainGroup))
    .GroupBy(r => r.ChainGroup!)
    .ToDictionary(g => g.Key, g => g.ToList());

var unchainedRules = syncConfig.Rules
    .Where(r => string.IsNullOrEmpty(r.ChainGroup))
    .ToList();

builder.Services.AddQuartz(q =>
{
    // Unchained rules: each gets its own cron and startup triggers
    foreach (var rule in unchainedRules)
    {
        var jobKey = new JobKey(rule.Name, "sync");
        q.AddJob<SyncJob>(opts => opts
            .WithIdentity(jobKey)
            .UsingJobData("RuleName", rule.Name)
            .StoreDurably());

        if (!string.IsNullOrEmpty(rule.CronExpression))
            q.AddTrigger(opts => opts
                .ForJob(jobKey)
                .WithIdentity($"{rule.Name}-trigger", "sync")
                .WithCronSchedule(rule.CronExpression, x => x.WithMisfireHandlingInstructionFireAndProceed()));

        if (rule.RunOnStartup)
            q.AddTrigger(opts => opts
                .ForJob(jobKey)
                .WithIdentity($"{rule.Name}-startup-trigger", "sync")
                .StartNow());
    }

    // Chained rules: only the first in each group gets direct triggers; the rest fire via chain
    foreach (var (_, rules) in chainGroups)
    {
        foreach (var (rule, index) in rules.Select((r, i) => (r, i)))
        {
            var jobKey = new JobKey(rule.Name, "sync");
            q.AddJob<SyncJob>(opts => opts
                .WithIdentity(jobKey)
                .UsingJobData("RuleName", rule.Name)
                .StoreDurably());

            if (index == 0)
            {
                if (!string.IsNullOrEmpty(rule.CronExpression))
                    q.AddTrigger(opts => opts
                        .ForJob(jobKey)
                        .WithIdentity($"{rule.Name}-trigger", "sync")
                        .WithCronSchedule(rule.CronExpression, x => x.WithMisfireHandlingInstructionFireAndProceed()));

                if (rule.RunOnStartup)
                    q.AddTrigger(opts => opts
                        .ForJob(jobKey)
                        .WithIdentity($"{rule.Name}-startup-trigger", "sync")
                        .StartNow());
            }
        }
    }
});

builder.Services.AddQuartzHostedService(options => options.WaitForJobsToComplete = true);

var host = builder.Build();

// Build a JobChainingJobListener per group so each chain runs sequentially
if (chainGroups.Count > 0)
{
    var schedulerFactory = host.Services.GetRequiredService<ISchedulerFactory>();
    var scheduler = await schedulerFactory.GetScheduler();

    foreach (var (group, rules) in chainGroups)
    {
        if (rules.Count < 2) continue;
        var chainListener = new JobChainingJobListener($"sync-chain-{group}");
        for (var i = 0; i < rules.Count - 1; i++)
            chainListener.AddJobChainLink(
                new JobKey(rules[i].Name, "sync"),
                new JobKey(rules[i + 1].Name, "sync"));
        scheduler.ListenerManager.AddJobListener(chainListener, GroupMatcher<JobKey>.GroupEquals("sync"));
    }
}

await host.RunAsync();
