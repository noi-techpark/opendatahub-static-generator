using System.Text.Json;
using System.Text.Json.Nodes;
using GeneratorWorker.Models;

namespace GeneratorWorker.Services;

/// <summary>Fetches all pages from a configured API endpoint and persists the result as a JSON file.</summary>
public class SyncService
{
    private static readonly JsonSerializerOptions PrettyPrint = new() { WriteIndented = true };

    private readonly ApiClientService _apiClient;
    private readonly ILogger<SyncService> _logger;

    public SyncService(ApiClientService apiClient, ILogger<SyncService> logger)
    {
        _apiClient = apiClient;
        _logger = logger;
    }

    public async Task SyncAsync(SyncRule rule, string outputPath, CancellationToken ct = default)
    {
        _logger.LogInformation("Sync started for rule '{Name}'", rule.Name);

        JsonNode? result;

        if (rule.Paging.Enabled)
        {
            result = await FetchAllPagesAsync(rule, ct);
        }
        else
        {
            var raw = await _apiClient.GetAsync(rule, new Dictionary<string, string>(), ct);
            var node = JsonNode.Parse(raw);

            result = !string.IsNullOrWhiteSpace(rule.Paging.DataPath)
                ? ResolvePath(node, rule.Paging.DataPath)?.DeepClone()
                : node;
        }

        var outputFile = ResolveOutputFile(outputPath, rule.OutputFileName);
        Directory.CreateDirectory(Path.GetDirectoryName(outputFile)!);
        await File.WriteAllTextAsync(outputFile, result?.ToJsonString(PrettyPrint) ?? "null", ct);

        var count = result is JsonArray arr ? arr.Count : 1;
        _logger.LogInformation("Sync complete for '{Name}': {Count} item(s) → {File}", rule.Name, count, outputFile);
    }

    private async Task<JsonArray> FetchAllPagesAsync(SyncRule rule, CancellationToken ct)
    {
        var paging = rule.Paging;
        var result = new JsonArray();
        var currentPage = paging.StartPage;
        int? totalPages = null;
        int? totalCount = null;

        while (true)
        {
            var pageParams = new Dictionary<string, string>
            {
                [paging.PageQueryParam] = currentPage.ToString()
            };

            if (!string.IsNullOrWhiteSpace(paging.PageSizeQueryParam))
                pageParams[paging.PageSizeQueryParam] = paging.PageSize.ToString();

            var raw = await _apiClient.GetAsync(rule, pageParams, ct);
            var responseNode = JsonNode.Parse(raw);
            var items = ExtractItems(responseNode, paging.DataPath);

            // Deep-clone items so they survive beyond this parse context
            foreach (var item in items)
                result.Add(JsonNode.Parse(item.ToJsonString()));

            // Resolve sentinels from the first response that contains them
            if (!string.IsNullOrWhiteSpace(paging.TotalPagesPath) && totalPages == null)
            {
                totalPages = ResolveInt(responseNode, paging.TotalPagesPath);
                if (totalPages.HasValue)
                    _logger.LogInformation("Rule '{Name}': {TotalPages} pages to fetch", rule.Name, totalPages.Value);
            }

            if (!string.IsNullOrWhiteSpace(paging.TotalCountPath) && totalCount == null)
                totalCount = ResolveInt(responseNode, paging.TotalCountPath);

            var progress = totalPages.HasValue
                ? $"{currentPage}/{totalPages}"
                : totalCount.HasValue
                    ? $"{result.Count}/{totalCount} items"
                    : $"page {currentPage}";

            _logger.LogInformation("Rule '{Name}': fetched {Progress} — {PageItems} item(s) this page, {Total} total",
                rule.Name, progress, items.Count, result.Count);

            if (items.Count == 0) break;

            if (totalPages.HasValue)
            {
                if (currentPage >= totalPages.Value) break;
            }
            else if (totalCount.HasValue)
            {
                if (result.Count >= totalCount.Value) break;
            }
            else if (!string.IsNullOrWhiteSpace(paging.HasMorePath))
            {
                if (!ResolveBool(responseNode, paging.HasMorePath)) break;
            }
            else if (items.Count < paging.PageSize)
            {
                // No sentinel available: a short page signals the last page
                break;
            }

            currentPage++;
        }

        return result;
    }

    // --- helpers ---

    private static List<JsonNode> ExtractItems(JsonNode? node, string? dataPath)
    {
        var target = string.IsNullOrWhiteSpace(dataPath) ? node : ResolvePath(node, dataPath);

        return target switch
        {
            JsonArray arr => arr.Select(n => n!).ToList(),
            not null => new List<JsonNode> { target },
            _ => new List<JsonNode>()
        };
    }

    private static JsonNode? ResolvePath(JsonNode? node, string path)
    {
        foreach (var segment in path.Split('.'))
        {
            if (node is JsonObject obj && obj.TryGetPropertyValue(segment, out var child))
                node = child;
            else
                return null;
        }
        return node;
    }

    private static int? ResolveInt(JsonNode? node, string path)
    {
        var n = ResolvePath(node, path);
        return n is JsonValue v && v.TryGetValue<int>(out var i) ? i : null;
    }

    private static bool ResolveBool(JsonNode? node, string path)
    {
        var n = ResolvePath(node, path);
        return n is JsonValue v && v.TryGetValue<bool>(out var b) && b;
    }

    private static string ResolveOutputFile(string outputPath, string fileName)
    {
        if (!fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            fileName += ".json";
        return Path.Combine(outputPath, fileName);
    }
}
