using GeneratorWorker.Models;
using GeneratorWorker.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

// --- Configuration ---
var configFile = GetNamedArg(args, "--config") ?? "appsettings.json";
var outputOverride = GetNamedArg(args, "--output");

var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile(configFile, optional: false, reloadOnChange: false)
    .AddJsonFile("appsettings.local.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

var syncConfig = configuration.GetSection("Sync").Get<SyncConfig>() ?? new SyncConfig();

if (syncConfig.Rules.Count == 0)
{
    Console.WriteLine("No sync rules found in configuration.");
    return 1;
}

// --- Services ---
var services = new ServiceCollection();
services.Configure<SyncConfig>(configuration.GetSection("Sync"));
services.AddHttpClient("sync");
services.AddSingleton<TokenCacheService>();
services.AddSingleton<RateLimiterService>();
services.AddScoped<ApiClientService>();
services.AddScoped<SyncService>();
services.AddLogging(b => b.AddConsole().SetMinimumLevel(LogLevel.Information));

await using var provider = services.BuildServiceProvider();

// --- Determine rules to run ---
var positionalArgs = args
    .Where(a => !a.StartsWith("--"))
    .ToArray();

List<SyncRule> rulesToRun;

if (positionalArgs.Length == 0)
{
    rulesToRun = PickInteractive(syncConfig.Rules);
    if (rulesToRun.Count == 0)
        return 0;
}
else if (positionalArgs[0].Equals("all", StringComparison.OrdinalIgnoreCase))
{
    rulesToRun = syncConfig.Rules;
}
else
{
    var name = positionalArgs[0];
    var rule = syncConfig.Rules.FirstOrDefault(r =>
        r.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    if (rule is null)
    {
        Console.WriteLine($"Rule '{name}' not found. Available rules:");
        foreach (var r in syncConfig.Rules)
            Console.WriteLine($"  {r.Name}");
        return 1;
    }

    rulesToRun = [rule];
}

// --- Run ---
var outputPath = Path.GetFullPath(outputOverride ?? syncConfig.OutputPath);
var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

foreach (var rule in rulesToRun)
{
    if (cts.Token.IsCancellationRequested) break;

    await using var scope = provider.CreateAsyncScope();
    var syncService = scope.ServiceProvider.GetRequiredService<SyncService>();
    await syncService.SyncAsync(rule, outputPath, cts.Token);
}

Console.WriteLine("\nDone.");
return 0;

// --- Helpers ---

static List<SyncRule> PickInteractive(List<SyncRule> rules)
{
    Console.WriteLine("Available sync rules:\n");
    Console.WriteLine("  [0] Run ALL");
    for (int i = 0; i < rules.Count; i++)
        Console.WriteLine($"  [{i + 1}] {rules[i].Name}  →  {rules[i].OutputFileName}.json");

    Console.Write("\nEnter number (or q to quit): ");
    var input = Console.ReadLine()?.Trim();

    if (string.IsNullOrEmpty(input) || input.Equals("q", StringComparison.OrdinalIgnoreCase))
        return [];

    if (!int.TryParse(input, out var choice) || choice < 0 || choice > rules.Count)
    {
        Console.WriteLine("Invalid selection.");
        return [];
    }

    return choice == 0 ? rules : [rules[choice - 1]];
}

static string? GetNamedArg(string[] args, string name)
{
    var prefix = name + "=";
    var arg = args.FirstOrDefault(a => a.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    return arg?[prefix.Length..];
}
