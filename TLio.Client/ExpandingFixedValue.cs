using TLio.Core.Contracts;
using TLio.Core.Models;

namespace TLio.Client;

/// <summary>
/// An IFunctionSupportedValue that holds a JSON object or array template
/// and expands embedded "=func()" string values within it at GetValue time.
///
/// Used by CommandConverter when a script "value" field is a JSON object or array.
/// String-valued properties/elements whose raw value starts with "=" are parsed
/// and evaluated via FunctionConverter just-in-time; all other nodes are returned
/// as-is (deep-cloned from the stored template).
///
/// This class lives in TLio.Client because FunctionConverter is only available here —
/// it cannot be in TLio.Core without creating a circular project reference.
/// </summary>
internal class ExpandingFixedValue<TNode> : IFunctionSupportedValue<TNode>
{
    private readonly TNode _template;
    private readonly FunctionConverter<TNode> _converter;
    private readonly INodeAdapter<TNode> _adapter;

    public ExpandingFixedValue(TNode template, FunctionConverter<TNode> converter, INodeAdapter<TNode> adapter)
    {
        _template = template;
        _converter = converter;
        _adapter = adapter;
    }

    public FunctionResult<TNode> GetValue(TNode currentNode, TNode dataContext, IExecutionContext<TNode> context)
    {
        var clone = _adapter.DeepClone(_template);
        Expand(clone, currentNode, dataContext, context);
        return FunctionResult<TNode>.Successful(clone);
    }

    public string ToScript() => "[expanding]";

    // ── Recursive expansion ───────────────────────────────────────────────────

    private void Expand(TNode node, TNode currentNode, TNode dataContext, IExecutionContext<TNode> context)
    {
        if (_adapter.IsObject(node))
        {
            foreach (var propName in _adapter.GetPropertyNames(node).ToList())
            {
                var child = _adapter.GetProperty(node, propName);
                if (child == null) continue;

                if (TryExpandString(child, currentNode, dataContext, context, out var expanded))
                    _adapter.SetProperty(node, propName, expanded!);
                else
                    Expand(child, currentNode, dataContext, context);
            }
        }
        else if (_adapter.IsArray(node))
        {
            var len = _adapter.GetArrayLength(node);
            for (int i = 0; i < len; i++)
            {
                var element = _adapter.GetArrayElement(node, i);
                if (TryExpandString(element, currentNode, dataContext, context, out var expanded))
                {
                    _adapter.RemoveFromArray(node, i);
                    _adapter.InsertIntoArray(node, i, expanded!);
                }
                else
                {
                    Expand(element, currentNode, dataContext, context);
                }
            }
        }
        // Primitives that are not "=" strings are already in the clone — no action needed.
    }

    /// <summary>
    /// If <paramref name="node"/> is a string primitive starting with "=",
    /// evaluate it as a function expression and write the computed value to
    /// <paramref name="expanded"/>. Returns true when expansion occurred.
    /// </summary>
    private bool TryExpandString(
        TNode node,
        TNode currentNode,
        TNode dataContext,
        IExecutionContext<TNode> context,
        out TNode? expanded)
    {
        expanded = default;
        if (!_adapter.IsPrimitive(node)) return false;

        var str = _adapter.TryGetString(node);
        if (str == null || !str.StartsWith("=")) return false;

        var valueParsed = _converter.ParseValue(str, _adapter);
        if (valueParsed == null) return false;

        var result = valueParsed.GetValue(currentNode, dataContext, context);
        if (!result.Success) return false;

        expanded = result.Data.First;
        return true;
    }
}
