using TLio.Client;
using TLio.Extensions.ETL;
using TLio.Extensions.Math;
using TLio.Extensions.Text;
using TLio.Extensions.TimeDate;

namespace TLio.Sample.Api;

/// <summary>
/// Builds the ParseOptions used by every endpoint in this sample.
///
/// The extension packs have to be registered here. A script that calls a function
/// from an unregistered pack fails as a whole, so leaving them out makes an
/// otherwise-valid script return no output at all rather than a partial result.
/// The CLI sample registers them; this one did not.
/// </summary>
internal static class EngineSetup
{
    public static ParseOptions<TNode> CreateOptions<TNode>()
    {
        var options = ParseOptions<TNode>.CreateDefault();
        options.FunctionsProvider.RegisterMath<TNode>();
        options.FunctionsProvider.RegisterText<TNode>();
        options.FunctionsProvider.RegisterTimeDate<TNode>();
        options.CommandsProvider.RegisterETL<TNode>();
        return options;
    }

    public static ScriptEngine<TNode> CreateEngine<TNode>()
    {
        var options = CreateOptions<TNode>();
        return new ScriptEngine<TNode>(options.CommandsProvider, options.FunctionsProvider);
    }
}
