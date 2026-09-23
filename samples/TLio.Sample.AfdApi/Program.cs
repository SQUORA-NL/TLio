using TLio.Sample.AfdApi;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

var scriptsDir = Path.Combine(AppContext.BaseDirectory, "Scripts");
var samplesDir = Path.Combine(AppContext.BaseDirectory, "SampleInput");
var runner = EngineSetup.CreateRunner();

string ReadScript(string name) => File.ReadAllText(Path.Combine(scriptsDir, name));

app.MapGet("/", () => Results.Ok(new
{
    service = "TLio AFD conversion demo",
    directions = new[]
    {
        new { path = "/convert/afd1-to-afd2", from = "AFD 1.0 (XML)", to = "AFD 2.0 (JSON)" },
        new { path = "/convert/afd1-to-afdshort", from = "AFD 1.0 (XML)", to = "AFD Short (JSON)" },
        new { path = "/convert/afdshort-to-afd2", from = "AFD Short (JSON)", to = "AFD 2.0 (JSON)" },
    },
    samples = Directory.Exists(samplesDir)
        ? Directory.GetFiles(samplesDir).Select(Path.GetFileName)
        : Array.Empty<string?>(),
}));

app.MapGet("/samples/{name}", (string name) =>
{
    var path = Path.Combine(samplesDir, name);
    if (!File.Exists(path))
        return Results.NotFound(new { error = $"No sample named '{name}'. See GET / for the list." });
    var contentType = name.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) ? "application/xml" : "application/json";
    return Results.Text(File.ReadAllText(path), contentType);
});

// AFD 1.0 (XML) -> AFD 2.0 (JSON): restructure + rename, one pass, one convert boundary.
app.MapPost("/convert/afd1-to-afd2", async (HttpRequest request) =>
    RunConversion(await ReadBody(request), "xml", ReadScript("afd1-to-afd2.tlio.json")));

// AFD 1.0 (XML) -> AFD Short (JSON): restructure only, AFD 1.0 labels kept.
app.MapPost("/convert/afd1-to-afdshort", async (HttpRequest request) =>
    RunConversion(await ReadBody(request), "xml", ReadScript("afd1-to-afdshort.tlio.json")));

// AFD Short (JSON) -> AFD 2.0 (JSON): rename-only, no format boundary.
app.MapPost("/convert/afdshort-to-afd2", async (HttpRequest request) =>
    RunConversion(await ReadBody(request), "json", ReadScript("afdshort-to-afd2.tlio.json")));

app.Run();
return;

static async Task<string> ReadBody(HttpRequest request)
{
    using var reader = new StreamReader(request.Body);
    return await reader.ReadToEndAsync();
}

IResult RunConversion(string inputDocument, string inputFormatId, string script)
{
    if (string.IsNullOrWhiteSpace(inputDocument))
        return Results.BadRequest(new { error = "Request body is empty." });

    // A conversion never aborts on data — an unmapped attribute, an unknown entity, a value that
    // fails a type conversion are all collected as issues, not thrown. Only a malformed script or
    // an unreadable input fails here, and then the input is returned unchanged with success=false
    // — that is the engine's contract, not this endpoint's.
    var result = runner.Run(inputFormatId, inputDocument, script);
    return Results.Content(result.Document, "application/json", statusCode: result.Success ? 200 : 422);
}
