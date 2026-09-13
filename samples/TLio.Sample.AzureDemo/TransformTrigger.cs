using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TLio.Sample.AzureDemo;

/// <summary>
/// POST /Transform — runs a TLio script against a JSON document.
///
/// Request body:  { "document": "&lt;json text&gt;", "commands": [ &lt;TLio command objects&gt; ] }
/// Response body: { "success": bool, "result": &lt;transformed document&gt;, "log": [ "&lt;entry&gt;", ... ] }
///
/// No auth, no queues, no database — this exists to be shown on a projector, not to run in
/// production.
///
/// Named "Trigger" rather than "Function" to keep it apart from TLio's own function concept
/// (<c>IFunction&lt;TNode&gt;</c>, <c>=datetime()</c> and friends) — this class is the Azure
/// Functions entry point, not a TLio function. The <c>[Function("Transform")]</c> attribute name
/// below is the Functions Worker SDK's own vocabulary and isn't ours to rename.
/// </summary>
public sealed class TransformTrigger
{
    private readonly TlioTransformer _transformer;
    private readonly ILogger<TransformTrigger> _logger;

    public TransformTrigger(TlioTransformer transformer, ILogger<TransformTrigger> logger)
    {
        _transformer = transformer;
        _logger = logger;
    }

    [Function("Transform")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "Transform")] HttpRequest req)
    {
        string body;
        using (var reader = new StreamReader(req.Body))
            body = await reader.ReadToEndAsync();

        JObject request;
        try
        {
            request = JObject.Parse(body);
        }
        catch (JsonException ex)
        {
            return BadRequest($"Request body is not valid JSON: {ex.Message}");
        }

        var document = request["document"]?.Value<string>();
        var commands = request["commands"] as JArray;

        if (string.IsNullOrWhiteSpace(document) || commands is null)
            return BadRequest("Request body must be { \"document\": <json text>, \"commands\": [ ... ] }.");

        var outcome = _transformer.Transform(document, commands.ToString(Formatting.None));

        if (!outcome.Success)
            _logger.LogWarning("Transform script reported failures: {Log}", string.Join(" | ", outcome.Log));

        var response = new JObject
        {
            ["success"] = outcome.Success,
            ["result"] = outcome.Result,
            ["log"] = new JArray(outcome.Log)
        };

        return Json(response, StatusCodes.Status200OK);
    }

    private static IActionResult BadRequest(string message) =>
        Json(new JObject { ["error"] = message }, StatusCodes.Status400BadRequest);

    private static ContentResult Json(JToken payload, int statusCode) => new()
    {
        Content = payload.ToString(Formatting.Indented),
        ContentType = "application/json",
        StatusCode = statusCode
    };
}
