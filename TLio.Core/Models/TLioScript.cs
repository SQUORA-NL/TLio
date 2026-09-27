using TLio.Core.Contracts;

namespace TLio.Core.Models;

/// <summary>
/// An ordered sequence of commands that constitutes a TLio script.
/// Execution runs each command in order, passing the (possibly mutated) data
/// result of one command as the context for the next.
/// </summary>
/// <typeparam name="TNode">Native node type of the target data format.</typeparam>
public class TLioScript<TNode> : List<ICommand<TNode>>
{
    /// <summary>
    /// Notation problems detected while parsing the script text (unknown nested function,
    /// <c>@field</c> written without the required dot, …). Parsing has no execution context
    /// to log into, so the warnings are carried here and replayed by <see cref="Execute"/>.
    /// </summary>
    public List<string> ParseWarnings { get; } = new();

    public TLioExecutionResult<TNode> Execute(TNode data, IExecutionContext<TNode> context)
    {
        foreach (var warning in ParseWarnings)
            context.LogWarning(CoreConstants.ScriptParsing, warning);

        var result = TLioExecutionResult<TNode>.Successful(data);
        foreach (var command in this)
        {
            result = command.Execute(result.Data, context);
            if (!result.Success) break;
        }
        return result;
    }

    /// <summary>
    /// Deep clone: every command cloned via its own <see cref="ICommand{TNode}.Clone"/> — which,
    /// for a command holding a nested <see cref="TLioScript{TNode}"/> of its own (a while/forEach
    /// body, an ifElse branch), recurses into cloning that nested script too. A nested script is
    /// "configuration" only in the sense that a script author never sees it change; its leaf
    /// commands still carry the same per-execution mutable state as any top-level command, so it
    /// must be cloned exactly like one — never shared across two independent executions.
    /// </summary>
    public TLioScript<TNode> Clone()
    {
        var clone = new TLioScript<TNode>();
        clone.AddRange(this.Select(c => c.Clone()));
        clone.ParseWarnings.AddRange(ParseWarnings);
        return clone;
    }

    public bool Validate() => this.All(c => c.ValidateCommandInstance().IsValid);

    public List<ValidationResult> GetValidationResults() =>
        this.Select(c => c.ValidateCommandInstance()).ToList();
}
