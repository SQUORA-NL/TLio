using TLio.Core.Contracts;
using TLio.Core.Models;

namespace TLio.Client;

/// <summary>
/// An IFunctionSupportedValue that holds a JSON object or array template
/// and expands embedded "=func()" string values within it at GetValue time.
///
/// Used by CommandConverter when a script "value" field is a JSON object or array.
/// String-valued properties/elements whose raw value starts with "=" are evaluated
/// per execution; all other nodes are returned as-is (deep-cloned from the stored template).
///
/// The expressions themselves are parsed <b>once</b>, when this value is constructed — the same
/// point at which a top-level "=func()" value is parsed by CommandConverter. Parsing them on
/// every GetValue instead meant a script that ran a thousand documents parsed the same
/// expression a thousand times; the text cannot have changed in between, because the template is
/// fixed at construction. What still happens per execution is the evaluation, which is the part
/// that depends on the document.
///
/// A template that holds no "=" string at all needs no walk either — <see cref="_plan"/> is
/// empty and GetValue returns the clone directly. Rate books and other constant blocks are the
/// common case, and they are the largest values in a script.
///
/// The plan is built in the constructor and never written to afterwards, so this class is as
/// safe to share as the parsed script that owns it — no more, no less. Two occurrences of the
/// same expression text share one parsed function instance, which is the arrangement a reused
/// script already has; functions hold their arguments and nothing else.
///
/// This class lives in TLio.Client because FunctionConverter is only available here —
/// it cannot be in TLio.Core without creating a circular project reference.
/// </summary>
internal class ExpandingFixedValue<TNode> : IFunctionSupportedValue<TNode>
{
    /// <summary>
    /// One embedded expression, parsed. <paramref name="Warnings"/> are the notation warnings
    /// the parse raised; they are replayed into the execution log at every expansion, because
    /// that is where they used to be written and a host reads them from there.
    /// </summary>
    private readonly record struct ParsedExpression(
        IFunctionSupportedValue<TNode>? Value,
        IReadOnlyList<string> Warnings);

    private readonly TNode _template;
    private readonly INodeAdapter<TNode> _adapter;
    private readonly Dictionary<string, ParsedExpression> _plan;

    public ExpandingFixedValue(TNode template, FunctionConverter<TNode> converter, INodeAdapter<TNode> adapter)
    {
        _template = template;
        _adapter = adapter;
        _plan = new Dictionary<string, ParsedExpression>(StringComparer.Ordinal);
        Plan(template, converter);
    }

    public FunctionResult<TNode> GetValue(TNode currentNode, TNode dataContext, IExecutionContext<TNode> context)
    {
        var clone = _adapter.DeepClone(_template);
        if (_plan.Count > 0)
            Expand(clone, currentNode, dataContext, context);
        return FunctionResult<TNode>.Successful(clone);
    }

    public string ToScript() => "[expanding]";

    /// <summary>
    /// The value as written, functions inside it still spelled "=func()" — which is exactly
    /// what a serializer needs to write it back out.
    /// </summary>
    internal TNode Template => _template;

    // ── Planning: walk the template once, parse each distinct expression ───────

    private void Plan(TNode node, FunctionConverter<TNode> converter)
    {
        if (_adapter.IsObject(node))
        {
            foreach (var propName in _adapter.GetPropertyNames(node).ToList())
            {
                var child = _adapter.GetProperty(node, propName);
                if (child != null) Plan(child, converter);
            }
        }
        else if (_adapter.IsArray(node))
        {
            var len = _adapter.GetArrayLength(node);
            for (var i = 0; i < len; i++)
                Plan(_adapter.GetArrayElement(node, i), converter);
        }
        else if (TryGetExpression(node, out var expression) && !_plan.ContainsKey(expression!))
        {
            var warnings = new List<string>();
            var parsed = converter.ParseValue(expression!, _adapter, warnings.Add);
            _plan[expression!] = new ParsedExpression(parsed, warnings);
        }
    }

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
    /// True when <paramref name="node"/> is a string primitive starting with "=" — the shape
    /// that <see cref="Plan"/> collects and <see cref="TryExpandString"/> replaces. Both ask the
    /// same question, so that the plan holds an entry for exactly the nodes the walk will look up.
    /// </summary>
    private bool TryGetExpression(TNode node, out string? expression)
    {
        expression = null;
        if (!_adapter.IsPrimitive(node)) return false;

        var str = _adapter.TryGetString(node);
        if (str == null || !str.StartsWith("=")) return false;

        expression = str;
        return true;
    }

    /// <summary>
    /// If <paramref name="node"/> is a string primitive starting with "=",
    /// evaluate the expression parsed for it at construction and write the computed value to
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
        if (!TryGetExpression(node, out var expression)) return false;
        if (!_plan.TryGetValue(expression!, out var planned)) return false;

        // The parse happened at construction; its warnings belong to this execution's log.
        foreach (var warning in planned.Warnings)
            context.LogWarning(TLio.Core.CoreConstants.ScriptParsing, warning);

        if (planned.Value == null) return false;

        var result = planned.Value.GetValue(currentNode, dataContext, context);
        if (!result.Success) return false;

        expanded = result.Data.First;
        return true;
    }
}
