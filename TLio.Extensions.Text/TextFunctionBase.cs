using System.Globalization;

namespace TLio.Extensions.Text;

/// <summary>
/// Base class for all text extension functions.
///
/// Provides helpers for resolving string and integer arguments, mirroring
/// the pattern established by MathFunctionBase in TLio.Extensions.Math.
///
/// Null-handling contract:
///   - Path not found (Data.Count == 0) → log error, return failure
///   - Path found, value is null → treated as empty string ""
/// </summary>
public abstract class TextFunctionBase<TNode> : FunctionBase<TNode>
{
    public override string FunctionName => TypeName(GetType()).ToLowerInvariant();

    // ── Argument resolution ───────────────────────────────────────────────────

    /// <summary>
    /// Resolve one argument to a string. Null found-but-null → "".
    /// Path not found → false + log error.
    /// </summary>
    protected static bool TryGetStringArg(
        IFunctionSupportedValue<TNode> arg,
        out string value,
        TNode currentNode, TNode dataContext,
        IExecutionContext<TNode> context, string funcName)
    {
        value = string.Empty;
        var result = ResolveArg(arg, currentNode, dataContext, context);
        if (!result.Success || result.Data.Count == 0)
        {
            context.LogError(funcName, $"{funcName}: argument path not found.");
            return false;
        }
        var node = result.Data.First!;
        if (context.NodeAdapter.IsNull(node)) return true; // found-null = ""
        value = context.NodeAdapter.TryGetString(node) ?? string.Empty;
        return true;
    }

    /// <summary>
    /// Resolve one argument to an integer. Null found-but-null → 0.
    /// Non-numeric string → false + log error.
    /// </summary>
    protected static bool TryGetIntArg(
        IFunctionSupportedValue<TNode> arg,
        out int value,
        TNode currentNode, TNode dataContext,
        IExecutionContext<TNode> context, string funcName)
    {
        value = 0;
        var result = ResolveArg(arg, currentNode, dataContext, context);
        if (!result.Success || result.Data.Count == 0)
        {
            context.LogError(funcName, $"{funcName}: argument path not found.");
            return false;
        }
        var node = result.Data.First!;
        if (context.NodeAdapter.IsNull(node)) return true;
        var num = context.NodeAdapter.TryGetDouble(node);
        if (num.HasValue) { value = (int)num.Value; return true; }
        var str = context.NodeAdapter.TryGetString(node);
        if (str != null && int.TryParse(str, out var parsed)) { value = parsed; return true; }
        context.LogError(funcName, $"{funcName}: non-integer argument.");
        return false;
    }

    /// <summary>
    /// Resolve one argument to a char (from its first character or numeric code).
    /// Returns ' ' (space) on null-found. Returns false on path-not-found.
    /// </summary>
    protected static bool TryGetCharArg(
        IFunctionSupportedValue<TNode> arg,
        out char value,
        TNode currentNode, TNode dataContext,
        IExecutionContext<TNode> context, string funcName)
    {
        value = ' ';
        var result = ResolveArg(arg, currentNode, dataContext, context);
        if (!result.Success || result.Data.Count == 0)
        {
            context.LogError(funcName, $"{funcName}: argument path not found.");
            return false;
        }
        var node = result.Data.First!;
        if (context.NodeAdapter.IsNull(node)) return true;
        var str = context.NodeAdapter.TryGetString(node);
        if (str != null && str.Length > 0) { value = str[0]; return true; }
        var num = context.NodeAdapter.TryGetDouble(node);
        if (num.HasValue) { value = (char)(int)num.Value; return true; }
        context.LogError(funcName, $"{funcName}: argument cannot be converted to a character.");
        return false;
    }

    /// <summary>
    /// Resolve one argument and collect all its string values into a list.
    /// Arrays are unwrapped element-by-element; null elements become "".
    /// Returns false on path-not-found.
    /// </summary>
    protected static bool TryCollectStrings(
        IFunctionSupportedValue<TNode> arg,
        List<string> values,
        TNode currentNode, TNode dataContext,
        IExecutionContext<TNode> context, string funcName)
    {
        var result = ResolveArg(arg, currentNode, dataContext, context);
        if (!result.Success || result.Data.Count == 0)
        {
            context.LogError(funcName, $"{funcName}: argument path not found.");
            return false;
        }
        foreach (var node in result.Data)
            CollectStringsFromNode(node, values, context.NodeAdapter);
        return true;
    }

    private static void CollectStringsFromNode(TNode node, List<string> values, INodeAdapter<TNode> adapter)
    {
        if (adapter.IsArray(node))
        {
            foreach (var el in adapter.GetArrayElements(node))
                CollectStringsFromNode(el, values, adapter);
            return;
        }
        values.Add(adapter.IsNull(node) ? string.Empty : (adapter.TryGetString(node) ?? string.Empty));
    }

    /// <summary>
    /// Resolve one argument, flatten its array value (if any) into a node list.
    /// Returns null on path-not-found.
    /// </summary>
    protected static List<TNode>? ResolveList(
        IFunctionSupportedValue<TNode> arg,
        TNode currentNode, TNode dataContext,
        IExecutionContext<TNode> context)
    {
        var result = ResolveArg(arg, currentNode, dataContext, context);
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
}
