using CShells.AspNetCore.Extensions;
using Newtonsoft.Json.Linq;
using Nuplane;
using Nuplane.Loading;
using Nuplane.Loading.Hosting.Builder;
using Nuplane.Sources.Directory.Configuration;
using TLio.Client;
using TLio.Json;
using TLio.Sample.DockerPlugin;

var builder = WebApplication.CreateBuilder(args);

// ── TLio ─────────────────────────────────────────────────────────────────────

var builtins = new FunctionsProvider<JToken>();
var defaultOptions = ParseOptions<JToken>.CreateDefault();
foreach (var name in defaultOptions.FunctionsProvider.GetRegisteredFunctionNames())
    builtins.Register(name, () => defaultOptions.FunctionsProvider.GetFunction(name)!);

builder.Services.AddSingleton<MutableFunctionsProvider<JToken>>(sp =>
    new MutableFunctionsProvider<JToken>(
        builtins,
        sp.GetRequiredService<ILogger<MutableFunctionsProvider<JToken>>>()));

builder.Services.AddSingleton<PluginCatalogService>();

// ── NuPlane ───────────────────────────────────────────────────────────────────

var pluginsPath = builder.Configuration["NUPLANE_PLUGINS_PATH"]
    ?? Environment.GetEnvironmentVariable("NUPLANE_PLUGINS_PATH")
    ?? "/plugins";

builder.Configuration["Nuplane:Sources:Directory:Path"] = pluginsPath;

var nuplaneConfig = builder.Configuration.GetSection("Nuplane");

builder.Services.AddNuplane(nuplaneConfig, nuplane =>
{
    nuplane.AddDirectoryFeedsFromConfiguration(nuplaneConfig);
    nuplane.AutoloadPackages(nuplaneConfig.GetSection("Loading"));
    nuplane.OnPackagesChanged<PluginLoader>();
});

// ── CShells ───────────────────────────────────────────────────────────────────

builder.Services.AddCShellsAspNetCore();

// ── Build ─────────────────────────────────────────────────────────────────────

var app = builder.Build();

app.MapShells();

// ── POST /transform/{format} ──────────────────────────────────────────────────

app.MapPost("/transform/{format}", async (
    string format,
    HttpRequest request,
    MutableFunctionsProvider<JToken> functionsProvider,
    ILogger<Program> log) =>
{
    using var reader = new StreamReader(request.Body);
    var body = await reader.ReadToEndAsync();

    if (string.IsNullOrWhiteSpace(body))
        return Results.BadRequest(new { error = "Request body must not be empty." });

    TransformRequest? payload;
    try { payload = System.Text.Json.JsonSerializer.Deserialize<TransformRequest>(body); }
    catch (Exception ex) { return Results.BadRequest(new { error = $"Invalid JSON: {ex.Message}" }); }

    if (payload is null || payload.Script is null)
        return Results.BadRequest(new { error = "Fields 'input' and 'script' are required." });

    var scriptJson = System.Text.Json.JsonSerializer.Serialize(payload.Script);

    var context = JsonExecutionContext.CreateDefault();
    var commandsProvider = ParseOptions<JToken>.CreateDefault().CommandsProvider;
    var engine = new ScriptEngine<JToken>(commandsProvider, functionsProvider);

    JToken input;
    try { input = JToken.Parse(System.Text.Json.JsonSerializer.Serialize(payload.Input)); }
    catch (Exception ex) { return Results.BadRequest(new { error = $"Invalid input document: {ex.Message}" }); }

    TLio.Core.Models.TLioExecutionResult<JToken> result;
    try { result = engine.Execute(scriptJson, input, context); }
    catch (Exception ex)
    {
        log.LogWarning(ex, "Script execution threw an exception.");
        return Results.Ok(new { success = false, error = ex.Message });
    }

    if (!result.Success)
    {
        var errors = context.GetLogEntries()
            .Select(e => e.Message)
            .ToArray();
        return Results.Ok(new { success = false, error = string.Join("; ", errors) });
    }

    return Results.Ok(new { success = true, data = result.Data });
});

// ── GET /plugins ──────────────────────────────────────────────────────────────

app.MapGet("/plugins", (
    PluginCatalogService catalogService,
    MutableFunctionsProvider<JToken> functionsProvider) =>
{
    var catalog = catalogService.GetCatalog();
    return Results.Ok(new
    {
        lastUpdated = catalog.LastUpdated,
        plugins = catalog.Entries.Select(e => new
        {
            packageId  = e.PackageId,
            loadedAt   = catalogService.GetAllPackages()
                             .FirstOrDefault(p => p.PackageId == e.PackageId)?.LoadedAt,
            functions  = e.Functions
        }),
        builtinFunctions = functionsProvider.GetBuiltinFunctionNames()
    });
});

// ── GET /plugins/status  (NuPlane load-state catalog) ────────────────────────

app.MapGet("/plugins/status", async (IPackageLoadStateCatalog loadStateCatalog, CancellationToken ct) =>
{
    var snapshot = await loadStateCatalog.GetLoadStateAsync(ct);
    return Results.Ok(new
    {
        entries = snapshot.Packages.Select(s => new
        {
            packageId    = s.PackageId,
            version      = s.Version,
            status       = s.Status.ToString(),
            loadedAt     = s.LoadedAtUtc,
            errorMessage = s.Diagnostics.FirstOrDefault()
        })
    });
});

// ── GET /health ───────────────────────────────────────────────────────────────

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

app.Run();

// ── Request model ─────────────────────────────────────────────────────────────

internal sealed record TransformRequest(
    object? Input,
    object[]? Script);
