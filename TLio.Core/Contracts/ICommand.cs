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

    /// <summary>Execute the command against the supplied data root.</summary>
    TLioExecutionResult<TNode> Execute(TNode dataContext, IExecutionContext<TNode> context);

    /// <summary>Validate the command's own configuration before execution.</summary>
    ValidationResult ValidateCommandInstance();
}
