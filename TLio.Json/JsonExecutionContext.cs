using Newtonsoft.Json.Linq;
using TLio.Core.Models;

namespace TLio.Json;

/// <summary>
/// Convenience factory for building an execution context wired up to the JSON adapters.
/// </summary>
public static class JsonExecutionContext
{
    public static ExecutionContext<JToken> CreateDefault() => new()
    {
        ItemsFetcher = new JsonPathItemsFetcher(),
        NodeAdapter = new JsonNodeAdapter(),
        Logger = new ExecutionLogger()
    };
}
