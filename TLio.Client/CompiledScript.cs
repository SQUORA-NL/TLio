using TLio.Core.Contracts;
using TLio.Core.Models;

namespace TLio.Client;

/// <summary>
/// An immutable parsed representation of a TLio script. Produced once via
/// <see cref="ScriptEngine{TNode}.Compile(string, INodeAdapter{TNode})"/>;
/// produces per-execution instances on demand via <see cref="CreateExecutable"/> or <see cref="Execute"/>.
///
/// Thread-safe: <see cref="CreateExecutable"/> and <see cref="Execute"/> may be called
/// concurrently from any number of threads. Each call returns an independent instance
/// that owns its own mutable execution state.
/// </summary>
/// <typeparam name="TNode">Native node type of the target data format.</typeparam>
public sealed class CompiledScript<TNode>
{
    private readonly TLioScript<TNode> _template;

    internal CompiledScript(TLioScript<TNode> template) => _template = template;

    /// <summary>
    /// How many commands the script text parsed to. Zero means nothing will happen when it
    /// runs — every parser answers text it cannot read with an empty script rather than an
    /// exception, so this is what tells a caller the compile found nothing.
    /// </summary>
    public int CommandCount => _template.Count;

    /// <summary>
    /// What the parser had to say about the script text — malformed input, a function name it
    /// did not recognise. Read alongside <see cref="CommandCount"/> to report why a compile
    /// produced nothing.
    /// </summary>
    public IReadOnlyList<string> ParseWarnings => _template.ParseWarnings;

    /// <summary>
    /// Returns a new <see cref="TLioScript{TNode}"/> with every command independently cloned.
    /// Each returned instance has its own execution state and must not be shared across threads.
    /// </summary>
    public TLioScript<TNode> CreateExecutable()
    {
        var script = new TLioScript<TNode>();
        script.AddRange(_template.Select(cmd => cmd.Clone()));
        script.ParseWarnings.AddRange(_template.ParseWarnings);
        return script;
    }

    /// <summary>
    /// Convenience: creates an executable instance and runs it against <paramref name="data"/>.
    /// Equivalent to <c>CreateExecutable().Execute(data, context)</c>.
    /// </summary>
    public TLioExecutionResult<TNode> Execute(TNode data, IExecutionContext<TNode> context) =>
        CreateExecutable().Execute(data, context);
}
