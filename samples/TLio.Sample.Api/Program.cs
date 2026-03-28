var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.Urls.Add("http://localhost:5100");

// ── Helper: read body and run TransformService ───────────────────────────────

static async Task<IResult> HandleTransform(string format, string contentType, HttpRequest request)
{
    using var reader = new StreamReader(request.Body);
    var payload = await reader.ReadToEndAsync();

    if (string.IsNullOrWhiteSpace(payload))
        return Results.BadRequest(new { error = "Request body must not be empty." });

    var result = TLio.Sample.Api.TransformService.Execute(format, payload);

    if (result is null)
        return Results.StatusCode(415); // unknown format — should not happen on named routes

    var (success, output, log) = result.Value;

    if (!success)
        return Results.UnprocessableEntity(new
        {
            error = "Transformation failed.",
            log   = log.Select(e => $"[{e.Level}] {e.Message}").ToArray()
        });

    return Results.Content(output, contentType);
}

// ── Endpoints ────────────────────────────────────────────────────────────────

app.MapPost("/transform/json", (HttpRequest req) =>
    HandleTransform("json", "application/json", req));

app.MapPost("/transform/xml", (HttpRequest req) =>
    HandleTransform("xml", "application/xml", req));

app.MapPost("/transform/yaml", (HttpRequest req) =>
    HandleTransform("yaml", "text/yaml", req));

app.Run();
