var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

var app = builder.Build();
app.UseCors();

var dataPath = Path.GetFullPath(
    app.Configuration["DataPath"] ?? Path.Combine(app.Environment.ContentRootPath, "..", "..", "data"));

IResult ServeFile(string? name)
{
    if (string.IsNullOrWhiteSpace(name))
        return Results.BadRequest("File name is required.");

    var safeName = Path.GetFileName(name);
    if (!safeName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        safeName += ".json";

    var fullPath = Path.GetFullPath(Path.Combine(dataPath, safeName));

    if (!fullPath.StartsWith(dataPath, StringComparison.OrdinalIgnoreCase))
        return Results.BadRequest("Invalid file path.");

    if (!File.Exists(fullPath))
        return Results.NotFound($"File '{safeName}' not found.");

    var content = File.ReadAllText(fullPath);
    return Results.Content(content, "application/json");
}

// GET /api?file=STAAccommodations_de
app.MapGet("/api", (string? file) => ServeFile(file));

// GET /api/STAAccommodations_de
app.MapGet("/api/{file}", (string file) => ServeFile(file));

// GET /api/list
app.MapGet("/api/list", () =>
{
    if (!Directory.Exists(dataPath))
        return Results.Ok(Array.Empty<string>());

    var files = Directory.GetFiles(dataPath, "*.json")
        .Select(Path.GetFileNameWithoutExtension)
        .OrderBy(f => f);

    return Results.Ok(files);
});

app.Run();
