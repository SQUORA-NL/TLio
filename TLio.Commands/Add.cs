using TLio.Commands.Logic;
using TLio.Core.Contracts;
using TLio.Core.Models;

namespace TLio.Commands;

/// <summary>
/// Adds a new property to an object node, or appends an element to an array node.
/// If the property already exists on an object, a warning is logged and the add is skipped.
/// Mirrors JLio's Add command exactly.
/// </summary>
public class Add<TNode> : PropertyChangeCommand<TNode>
{
    public Add() { }

    public Add(string path, IFunctionSupportedValue<TNode> value)
    {
        Path = path;
        Value = value;
    }

    public Add(string path, string property, IFunctionSupportedValue<TNode> value)
    {
        Path = path;
        Property = property;
        Value = value;
    }

    protected override void ApplyValueToTarget(
        string propertyName, TNode targetNode, TNode value,
        TNode dataContext, IExecutionContext<TNode> context)
    {
        if (context.NodeAdapter.IsObject(targetNode) &&
            context.NodeAdapter.HasProperty(targetNode, propertyName))
        {
            // JLio Add logs a warning and skips when property already exists
            context.LogWarning(TLio.Core.CoreConstants.CommandExecution,
                $"{CommandName}: property '{propertyName}' already exists, skipping");
            return;
        }

        AddProperty(propertyName, targetNode, value, context);
    }
}
