using TLio.Core.Models;

namespace TLio.Core.Contracts;

/// <summary>
/// A command defines WHAT to do (e.g. set, add, remove a node) independently of
/// the underlying data format. All format-specific work is delegated to the
/// IItemsFetcher and INodeAdapter that live on the execution context.
/// </summary>
/// <typeparam name="TNode">The native node type of the target data format (e.g. JToken, XElement, YamlNode).</typeparam>
public interface ICommand<TNode>
{
    /// <summary>Canonical name used in script serialisation.</summary>
    string CommandName { get; }

    /// <summary>
    /// Optional free-text title a script author gives this step ("normalise the driver block").
    /// Documentation only: nothing in the engine reads it, and a script behaves identically with
    /// it and without it. Null when the script did not write one.
    /// </summary>
    string? Title => null;

    /// <summary>
    /// Optional free-text description — the longer half of <see cref="Title"/>: why the step is
    /// there, what it assumes, what it deliberately leaves alone. Documentation only, exactly as
    /// <see cref="Title"/> is. Null when the script did not write one.
    /// </summary>
    string? Description => null;

    /// <summary>Execute the command against the supplied data root.</summary>
    TLioExecutionResult<TNode> Execute(TNode dataContext, IExecutionContext<TNode> context);

    /// <summary>Validate the command's own configuration before execution.</summary>
    ValidationResult ValidateCommandInstance();

    /// <summary>
    /// Returns an independent copy of this command with its own mutable execution state.
    /// Configuration properties set at parse time are shared (read-only during execution).
    /// Commands derived from <see cref="CommandBase{TNode}"/> inherit a working implementation via MemberwiseClone.
    /// External implementations that do not derive from CommandBase must override this to use CompiledScript.
    /// </summary>
    ICommand<TNode> Clone() =>
        throw new NotSupportedException(
            $"{GetType().Name} does not implement Clone(). " +
            "Derive from CommandBase<TNode> or override Clone() to use CompiledScript<TNode>.");
}
