using Quartz;
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
    foreach (var rule in syncConfig.Rules)
    {
        var jobKey = new JobKey(rule.Name, "sync");

        q.AddJob<SyncJob>(opts => opts
            .WithIdentity(jobKey)
            .UsingJobData("RuleName", rule.Name)
            .StoreDurably());

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
});

builder.Services.AddQuartzHostedService(options => options.WaitForJobsToComplete = true);

var host = builder.Build();
host.Run();
