using CShells.AspNetCore.Extensions;
using Newtonsoft.Json.Linq;
using Nuplane;
using Nuplane.Loading;
using Nuplane.Loading.Hosting.Builder;
using Nuplane.Sources.Directory.Builder;
using TLio.Client;
using TLio.Json;
using TLio.Sample.DockerPlugin;
using TLio.Sample.DockerPlugin.Endpoints;
using TLio.Sample.DockerPlugin.Registry;
using TLio.Sample.DockerPlugin.Services;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(o =>
    o.Limits.MaxRequestBodySize = builder.Configuration.GetValue<long?>("SlugCache:MaxBodySizeBytes") ?? 10 * 1024 * 1024);

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
builder.Services.AddSingleton<PluginFileLoader>();

// ── Slug-cache services ───────────────────────────────────────────────────────

builder.Services.AddSingleton<IScriptRegistry, ScriptRegistry>();
builder.Services.AddSingleton<ScriptCompiler>();
builder.Services.AddSingleton<FormatDetector>();
builder.Services.AddSingleton<RegistrationPayloadParser>();
builder.Services.AddSingleton<StartupScriptLoader>();

// ── NuPlane ───────────────────────────────────────────────────────────────────

var pluginsPath = builder.Configuration["NUPLANE_PLUGINS_PATH"]
    ?? Environment.GetEnvironmentVariable("NUPLANE_PLUGINS_PATH")
    ?? "/plugins";

builder.Configuration["Nuplane:Sources:Directory:Path"] = pluginsPath;

var nuplaneConfig = builder.Configuration.GetSection("Nuplane");

builder.Services.AddNuplane(nuplaneConfig, nuplane =>
{
    nuplane.AddDirectoryFeed("plugins", pluginsPath, cfg =>
    {
        cfg.Watch = true;
        cfg.IncludeAll();
    });
    nuplane.AutoloadPackages(nuplaneConfig.GetSection("Loading"), lb => lb.Enable());
    nuplane.OnPackagesChanged<PluginLoader>();
});

// ── CShells ───────────────────────────────────────────────────────────────────

builder.Services.AddCShellsAspNetCore();

// ── Build ─────────────────────────────────────────────────────────────────────

var app = builder.Build();

app.MapShells();

// ── Slug-cache endpoints ──────────────────────────────────────────────────────

SlugExecutionEndpoints.Map(app);
ScriptManagementEndpoints.Map(app);

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
    try { payload = System.Text.Json.JsonSerializer.Deserialize<TransformRequest>(body, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true }); }
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

    var resultJson = System.Text.Json.JsonDocument.Parse(result.Data.ToString(Newtonsoft.Json.Formatting.None));
    return Results.Ok(new { success = true, data = resultJson.RootElement });
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

// ── POST /plugins/reload ──────────────────────────────────────────────────────

app.MapPost("/plugins/reload", async (
    PluginFileLoader loader,
    IConfiguration config,
    CancellationToken ct) =>
{
    var pluginsPath = config["NUPLANE_PLUGINS_PATH"] ?? "/plugins";
    var result = await loader.ReloadAsync(pluginsPath, ct);
    return Results.Ok(new { loaded = result.Loaded, unloaded = result.Unloaded, errors = result.Errors });
});

// ── GET /health ───────────────────────────────────────────────────────────────

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

// ── Startup: seed registry from config ───────────────────────────────────────

using (var scope = app.Services.CreateScope())
{
    var loader  = scope.ServiceProvider.GetRequiredService<StartupScriptLoader>();
    var reg     = scope.ServiceProvider.GetRequiredService<IScriptRegistry>();
    var comp    = scope.ServiceProvider.GetRequiredService<ScriptCompiler>();
    var logger  = scope.ServiceProvider.GetRequiredService<ILogger<StartupScriptLoader>>();
    var config  = scope.ServiceProvider.GetRequiredService<IConfiguration>();
    await loader.LoadAsync(reg, comp, logger, config);
}

app.Run();

// ── Request model ─────────────────────────────────────────────────────────────

internal sealed record TransformRequest(
    object? Input,
    object[]? Script);

public partial class Program { }
