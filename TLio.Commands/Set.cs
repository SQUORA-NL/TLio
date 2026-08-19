using TLio.Commands.Logic;
using TLio.Core.Contracts;
using TLio.Core.Models;

namespace TLio.Commands;

/// <summary>
/// Sets the value of a node at the given path.
/// The node must already exist; Set never creates new properties (use Put for upsert).
/// Mirrors JLio's Set command exactly.
/// </summary>
public class Set<TNode> : PropertyChangeCommand<TNode>
{
    public Set() { }

    public Set(string path, IFunctionSupportedValue<TNode> value)
    {
        Path = path;
        Value = value;
    }

    public Set(string path, string property, IFunctionSupportedValue<TNode> value)
    {
        Path = path;
        Property = property;
        Value = value;
    }

    protected override bool CreatesMissingPath => false;

    protected override void ApplyValueToTarget(
        string propertyName, TNode targetNode, TNode value,
        TNode dataContext, IExecutionContext<TNode> context)
    {
        ReplaceProperty(propertyName, targetNode, value, context);
    }
}
