using TLio.Core.Contracts;

namespace TLio.Core.Models;

/// <summary>
/// Convenience base class for commands. Provides tracking of execution success
/// and the conventional CommandName derivation. Concrete commands only need to
/// implement Execute() and ValidateCommandInstance().
/// </summary>
/// <typeparam name="TNode">Native node type of the target data format.</typeparam>
public abstract class CommandBase<TNode> : ICommand<TNode>
{
    private bool _executionFailed;

    /// <summary>
    /// Derived from the class name by default (e.g. "set", "add").
    /// Strips generic arity suffix (`1, `2 …) and camelCases the first letter.
    /// Override in subclasses to provide a custom script token.
    /// </summary>
    public virtual string CommandName
    {
        get
        {
            var name = GetType().Name;
            var backtickIdx = name.IndexOf('`');
            var baseName = backtickIdx >= 0 ? name.Substring(0, backtickIdx) : name;
            return char.ToLowerInvariant(baseName[0]) + baseName.Substring(1);
        }
    }

    /// <summary>
    /// Free text from the script, carried so tooling and readers can see it. Set by the script
    /// parsers from the "title" field in whichever notation the script is written in; never read
    /// during execution.
    /// </summary>
    public string? Title { get; set; }

    /// <inheritdoc cref="Title" />
    public string? Description { get; set; }

    public abstract TLioExecutionResult<TNode> Execute(TNode dataContext, IExecutionContext<TNode> context);

    public abstract ValidationResult ValidateCommandInstance();

    protected void MarkFailed() => _executionFailed = true;
    protected void ResetSuccess() => _executionFailed = false;
    protected bool IsSuccessful => !_executionFailed;

    // _executionFailed is a bool (value type) so each clone gets its own independent flag.
    // Configuration properties set at parse time are reference-copied but read-only during execution.
    public ICommand<TNode> Clone() => (ICommand<TNode>)MemberwiseClone();
}
