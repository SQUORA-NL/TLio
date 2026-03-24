using TLio.Commands.Logic;
using TLio.Core.Contracts;
using TLio.Core.Models;

namespace TLio.Commands;

/// <summary>
/// Sets the value if the node exists, or creates it if it does not (upsert).
/// Mirrors JLio's Put command exactly.
/// </summary>
public class Put<TNode> : PropertyChangeCommand<TNode>
{
    public Put() { }

    public Put(string path, IFunctionSupportedValue<TNode> value)
    {
        Path = path;
        Value = value;
    }

    public Put(string path, string property, IFunctionSupportedValue<TNode> value)
    {
        Path = path;
        Property = property;
        Value = value;
    }

    protected override void ApplyValueToTarget(
        string propertyName, TNode targetNode, TNode value,
        TNode dataContext, IExecutionContext<TNode> context)
    {
        UpsertProperty(propertyName, targetNode, value, context);
    }
}
