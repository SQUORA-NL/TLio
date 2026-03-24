using System.Text.Json.Nodes;
using TLio.Core.Models;

namespace TLio.Json.SystemText;

/// <summary>
/// Convenience factory for building an execution context wired up to the
/// System.Text.Json adapters.
/// </summary>
public static class SystemTextJsonExecutionContext
{
    public static ExecutionContext<JsonNode> CreateDefault() => new()
    {
        ItemsFetcher = new SystemTextJsonPathItemsFetcher(),
        NodeAdapter = new SystemTextJsonNodeAdapter(),
        Logger = new ExecutionLogger()
    };
}
