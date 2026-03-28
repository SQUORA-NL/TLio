using TLio.Core.Models;
using YamlDotNet.RepresentationModel;

namespace TLio.Yaml;

/// <summary>
/// Convenience factory for building an execution context wired up to the YAML adapters.
/// A single <see cref="YamlParentTracker"/> is created and shared between the node
/// adapter and the path fetcher so that parent relationships discovered during path
/// traversal are visible to Replace/RemoveFromParent operations.
/// </summary>
public static class YamlExecutionContext
{
    public static ExecutionContext<YamlNode> CreateDefault()
    {
        var tracker = new YamlParentTracker();
        return new()
        {
            ItemsFetcher = new YamlPathItemsFetcher(tracker),
            NodeAdapter  = new YamlNodeAdapter(tracker),
            Logger       = new ExecutionLogger()
        };
    }
}
