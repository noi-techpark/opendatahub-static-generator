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

builder.Services.AddQuartz(q =>
{
    foreach (var (rule, index) in syncConfig.Rules.Select((r, i) => (r, i)))
    {
        var jobKey = new JobKey(rule.Name, "sync");

        q.AddJob<SyncJob>(opts => opts
            .WithIdentity(jobKey)
            .UsingJobData("RuleName", rule.Name)
            .StoreDurably());

        // Only the first rule gets direct triggers; the rest fire via job chaining
        if (index == 0)
        {
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
});

builder.Services.AddQuartzHostedService(options => options.WaitForJobsToComplete = true);

var host = builder.Build();

// Chain jobs sequentially: when each job completes it fires the next one in order
if (syncConfig.Rules.Count > 1)
{
    var schedulerFactory = host.Services.GetRequiredService<ISchedulerFactory>();
    var scheduler = await schedulerFactory.GetScheduler();
    var chainListener = new JobChainingJobListener("sync-chain");
    for (var i = 0; i < syncConfig.Rules.Count - 1; i++)
        chainListener.AddJobChainLink(
            new JobKey(syncConfig.Rules[i].Name, "sync"),
            new JobKey(syncConfig.Rules[i + 1].Name, "sync"));
    scheduler.ListenerManager.AddJobListener(chainListener, GroupMatcher<JobKey>.GroupEquals("sync"));
}

await host.RunAsync();
