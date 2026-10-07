using System.Text.Json.Nodes;
using TLio.Core.Models;
using TLio.JsonPath;

namespace TLio.Json.SystemText;

/// <summary>
/// Convenience factory for building an execution context wired up to the
/// System.Text.Json adapters.
/// </summary>
public static class SystemTextJsonExecutionContext
{
    /// <summary>A context whose paths are read in the Newtonsoft dialect — identical to <c>TLio.Json</c>.</summary>
    public static ExecutionContext<JsonNode> CreateDefault() => Create(new SystemTextJsonPathItemsFetcher());

    /// <summary>
    /// A context whose paths are read in <paramref name="dialect"/>: <see cref="JsonPathDialect.Newtonsoft"/> (the default),
    /// <see cref="JsonPathDialect.Rfc9535"/>, or <see cref="JsonPathDialect.Extended"/> (Newtonsoft plus RFC 9535 where
    /// Newtonsoft has no answer).
    /// </summary>
    public static ExecutionContext<JsonNode> CreateDefault(JsonPathDialect dialect) => Create(new SystemTextJsonPathItemsFetcher(dialect));

    /// <summary>A context backed by a configured <see cref="JsonPathEngine"/> (custom functions, regex timeout, limits, …).</summary>
    public static ExecutionContext<JsonNode> Create(JsonPathEngine engine) => Create(new SystemTextJsonPathItemsFetcher(engine));

    private static ExecutionContext<JsonNode> Create(SystemTextJsonPathItemsFetcher fetcher) => new()
    {
        ItemsFetcher = fetcher,
        NodeAdapter = new SystemTextJsonNodeAdapter(),
        Logger = new ExecutionLogger()
    };
}
