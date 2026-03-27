using System.Globalization;
using TLio.Core.Contracts;
using TLio.Core.Models;

namespace TLio.Extensions.Math;

/// <summary>
/// Base class for all math extension functions.
///
/// Provides helpers for:
///   - <see cref="CreateNumericResult"/> — produces integer (<see langword="long"/>) output for whole
///     numbers and floating-point output for fractional results, mirroring JLio's MathHelper.
///   - <see cref="TryAccumulateArg"/> — resolves one function argument and adds all its numeric
///     values to a running total, handling arrays, null, and numeric strings.
///   - <see cref="TryCollectArg"/> — same resolution logic but collects into a <see cref="List{T}"/>
///     (used by Median, Min, Max).
///   - <see cref="TryGetSingleArg"/> — resolves exactly one scalar value from an argument
///     (used by Abs, Ceil, Floor, Round, Sqrt, Pow, Modulo).
///
/// Null-handling contract (mirrors JLio):
///   - Path not found (Data.Count == 0) → log error, return <see langword="false"/>
///   - Path found, value is null (IsNull == true) → treat as 0, continue
///   - Array elements are always "found" regardless of their value
/// </summary>
public abstract class MathFunctionBase<TNode> : FunctionBase<TNode>
{
    // Derive function name from class name (e.g. SumIfs → "sumifs")
    public override string FunctionName => GetType().Name.ToLowerInvariant();

    // ── Numeric result creation ───────────────────────────────────────────────

    /// <summary>
    /// Returns a whole-number node (<see langword="long"/>) when <paramref name="value"/> has no
    /// fractional part, and a floating-point node otherwise.
    /// Mirrors JLio's <c>MathHelper.CreateNumericValue</c>.
    /// </summary>
    protected static TNode CreateNumericResult(double value, INodeAdapter<TNode> adapter)
    {
        if (!double.IsNaN(value) && !double.IsInfinity(value)
            && value == System.Math.Floor(value)
            && value >= long.MinValue && value <= long.MaxValue)
        {
            return adapter.CreateValue((long)value);
        }
        return adapter.CreateNumber(value);
    }

    // ── Argument accumulation ─────────────────────────────────────────────────

    /// <summary>
    /// Resolve one function argument and add ALL its numeric values to <paramref name="total"/>.
    /// Returns <see langword="false"/> (and logs an error) if the path was not found or a
    /// non-numeric, non-null node is encountered.
    /// </summary>
    protected static bool TryAccumulateArg(
        IFunctionSupportedValue<TNode> arg,
        ref double total,
        TNode currentNode, TNode dataContext,
        IExecutionContext<TNode> context, string funcName)
    {
        var result = arg.GetValue(currentNode, dataContext, context);
        if (!result.Success || result.Data.Count == 0)
        {
            context.LogError(funcName, $"{funcName}: argument path not found.");
            return false;
        }
        foreach (var node in result.Data)
            if (!TryAddNode(node, ref total, context.NodeAdapter, context, funcName))
                return false;
        return true;
    }

    /// <summary>
    /// Resolve one argument and collect ALL its numeric values into <paramref name="values"/>.
    /// Returns <see langword="false"/> on path-not-found or non-numeric nodes.
    /// </summary>
    protected static bool TryCollectArg(
        IFunctionSupportedValue<TNode> arg,
        List<double> values,
        TNode currentNode, TNode dataContext,
        IExecutionContext<TNode> context, string funcName)
    {
        var result = arg.GetValue(currentNode, dataContext, context);
        if (!result.Success || result.Data.Count == 0)
        {
            context.LogError(funcName, $"{funcName}: argument path not found.");
            return false;
        }
        foreach (var node in result.Data)
            if (!TryCollectNode(node, values, context.NodeAdapter, context, funcName))
                return false;
        return true;
    }

    /// <summary>
    /// Resolve one argument and return its single scalar numeric value.
    /// Null (found-but-null) maps to 0. Path-not-found returns <see langword="false"/>.
    /// </summary>
    protected static bool TryGetSingleArg(
        IFunctionSupportedValue<TNode> arg,
        out double value,
        TNode currentNode, TNode dataContext,
        IExecutionContext<TNode> context, string funcName)
    {
        value = 0;
        var result = arg.GetValue(currentNode, dataContext, context);
        if (!result.Success || result.Data.Count == 0)
        {
            context.LogError(funcName, $"{funcName}: argument path not found.");
            return false;
        }
        var node = result.Data.First!;
        if (context.NodeAdapter.IsNull(node)) return true; // found-null = 0
        var num = context.NodeAdapter.TryGetDouble(node);
        if (num.HasValue) { value = num.Value; return true; }
        var str = context.NodeAdapter.TryGetString(node);
        if (str != null && double.TryParse(str, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
        {
            value = parsed;
            return true;
        }
        context.LogError(funcName, $"{funcName}: non-numeric argument.");
        return false;
    }

    // ── Private traversal helpers ─────────────────────────────────────────────

    private static bool TryAddNode(
        TNode node, ref double total,
        INodeAdapter<TNode> adapter, IExecutionContext<TNode> context, string funcName)
    {
        if (adapter.IsNull(node)) return true;          // found-null = treat as 0
        if (adapter.IsArray(node))
        {
            foreach (var el in adapter.GetArrayElements(node))
                if (!TryAddNode(el, ref total, adapter, context, funcName))
                    return false;
            return true;
        }
        var num = adapter.TryGetDouble(node);
        if (num.HasValue) { total += num.Value; return true; }
        var str = adapter.TryGetString(node);
        if (str != null && double.TryParse(str, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
        {
            total += parsed;
            return true;
        }
        context.LogError(funcName, $"{funcName}: non-numeric value encountered.");
        return false;
    }

    private static bool TryCollectNode(
        TNode node, List<double> values,
        INodeAdapter<TNode> adapter, IExecutionContext<TNode> context, string funcName)
    {
        if (adapter.IsNull(node)) { values.Add(0); return true; } // found-null = 0
        if (adapter.IsArray(node))
        {
            foreach (var el in adapter.GetArrayElements(node))
                if (!TryCollectNode(el, values, adapter, context, funcName))
                    return false;
            return true;
        }
        var num = adapter.TryGetDouble(node);
        if (num.HasValue) { values.Add(num.Value); return true; }
        var str = adapter.TryGetString(node);
        if (str != null && double.TryParse(str, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
        {
            values.Add(parsed);
            return true;
        }
        context.LogError(funcName, $"{funcName}: non-numeric value encountered.");
        return false;
    }

    // ── Conditional helpers (used by SumIf/Ifs, CountIf/Ifs, etc.) ────────────

    /// <summary>
    /// Extract all elements from a node as a flat list. If the node is an array, returns
    /// its elements; otherwise wraps the single node in a one-element list.
    /// </summary>
    protected static List<TNode> ExtractList(TNode node, INodeAdapter<TNode> adapter)
    {
        if (adapter.IsArray(node))
            return adapter.GetArrayElements(node).ToList();
        return new List<TNode> { node };
    }

    /// <summary>
    /// Resolve an argument and flatten its value into a list of nodes.
    /// Returns null when the path is not found.
    /// </summary>
    protected static List<TNode>? ResolveList(
        IFunctionSupportedValue<TNode> arg,
        TNode currentNode, TNode dataContext,
        IExecutionContext<TNode> context)
    {
        var result = arg.GetValue(currentNode, dataContext, context);
        if (!result.Success || result.Data.Count == 0) return null;

        var list = new List<TNode>();
        foreach (var node in result.Data)
        {
            if (context.NodeAdapter.IsArray(node))
                list.AddRange(context.NodeAdapter.GetArrayElements(node));
            else
                list.Add(node);
        }
        return list;
    }

    /// <summary>
    /// Extract the primitive value of a node as an object suitable for
    /// <see cref="ConditionEvaluator.EvaluateCondition"/>.
    /// </summary>
    protected static object? ExtractPrimitiveValue(TNode node, INodeAdapter<TNode> adapter)
    {
        if (adapter.IsNull(node)) return null;
        var num = adapter.TryGetDouble(node);
        if (num.HasValue) return num.Value;
        return adapter.TryGetString(node);
    }

    /// <summary>Extract the criteria string from a node, stripping surrounding single quotes.</summary>
    protected static string ExtractCriteria(TNode node, INodeAdapter<TNode> adapter)
    {
        var s = adapter.TryGetString(node) ?? string.Empty;
        if (s.StartsWith("'") && s.EndsWith("'") && s.Length >= 2)
            s = s[1..^1];
        return s;
    }
}
