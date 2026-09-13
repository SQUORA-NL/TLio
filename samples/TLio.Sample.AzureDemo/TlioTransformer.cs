using Newtonsoft.Json.Linq;
using TLio.Client;
using TLio.Json;

namespace TLio.Sample.AzureDemo;

/// <summary>
/// Wraps a single, shared <see cref="ScriptEngine{TNode}"/> for JSON documents. Registered as a
/// singleton in <c>Program.cs</c> — the engine's command/function registries are immutable after
/// construction, so one instance safely serves every concurrent request.
/// </summary>
public sealed class TlioTransformer
{
    private readonly ScriptEngine<JToken> _engine;

    public TlioTransformer()
    {
        var options = ParseOptions<JToken>.CreateDefault();
        _engine = new ScriptEngine<JToken>(options.CommandsProvider, options.FunctionsProvider);
    }

    /// <param name="documentJson">The input document, as JSON text.</param>
    /// <param name="commandsJson">A TLio script — a JSON array of command objects — as text.</param>
    public TlioTransformResult Transform(string documentJson, string commandsJson)
    {
        var context = JsonExecutionContext.CreateDefault();

        JToken document;
        try
        {
            document = context.NodeAdapter.Parse(documentJson);
        }
        catch (Exception ex)
        {
            return new TlioTransformResult(false, JValue.CreateNull(), new[] { $"Could not parse 'document' as JSON: {ex.Message}" });
        }

        var result = _engine.Execute(commandsJson, document, context);
        var log = context.GetLogEntries()
            .Select(entry => $"[{entry.Level}] {entry.Group}: {entry.Message}")
            .ToArray();

        return new TlioTransformResult(result.Success, result.Data, log);
    }
}

public sealed record TlioTransformResult(bool Success, JToken Result, IReadOnlyList<string> Log);
