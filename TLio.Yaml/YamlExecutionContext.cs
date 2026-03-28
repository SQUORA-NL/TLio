using TLio.Core.Models;
using YamlDotNet.RepresentationModel;

namespace TLio.Yaml;

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
