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
    /// Derived from the class name by default (e.g. "Set", "Add").
    /// Override in subclasses to provide a custom script token.
    /// </summary>
    public virtual string CommandName => GetType().Name;

    public abstract TLioExecutionResult<TNode> Execute(TNode dataContext, IExecutionContext<TNode> context);

    public abstract ValidationResult ValidateCommandInstance();

    protected void MarkFailed() => _executionFailed = true;
    protected void ResetSuccess() => _executionFailed = false;
    protected bool IsSuccessful => !_executionFailed;
}
