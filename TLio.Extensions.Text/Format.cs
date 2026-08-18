using System.Globalization;
using TLio.Core.Contracts;

namespace TLio.Extensions.Text;

/// <summary>
/// =format(template, arg1, arg2, ...) — string format using {0}, {1}, ... placeholders.
///
/// Numeric and boolean arguments are passed with their own type, so standard .NET format
/// specifiers work: <c>=format('{0:F2}', $.price)</c> → <c>"14.50"</c>,
/// <c>=format('{0:00000}', $.id)</c> → <c>"00042"</c>. Everything else is passed as text.
///
/// Formatting uses the invariant culture, so output does not change with the machine's
/// locale. Use =toFixed(value, decimals, ',') when you want a locale-style separator.
/// </summary>
public class Format<TNode> : TextFunctionBase<TNode>
{
    public override FunctionResult<TNode> Execute(TNode currentNode, TNode dataContext, IExecutionContext<TNode> context)
    {
        if (Arguments.Count == 0)
        {
            context.LogWarning(FunctionName, $"{FunctionName}: at least one argument required (template).");
            return FunctionResult<TNode>.Failed(currentNode);
        }
        if (!TryGetStringArg(Arguments[0], out var template, currentNode, dataContext, context, FunctionName))
            return FunctionResult<TNode>.Failed(currentNode);

        var args = new List<object?>();
        for (int i = 1; i < Arguments.Count; i++)
        {
            var resolved = ResolveArg(Arguments[i], currentNode, dataContext, context);
            if (!resolved.Success || resolved.Data.Count == 0)
            {
                context.LogError(FunctionName, $"{FunctionName}: argument path not found.");
                return FunctionResult<TNode>.Failed(currentNode);
            }
            args.Add(AsFormattable(resolved.Data.First!, context.NodeAdapter));
        }

        try
        {
            var formatted = string.Format(CultureInfo.InvariantCulture, template, args.ToArray());
            return FunctionResult<TNode>.Successful(context.NodeAdapter.CreateString(formatted));
        }
        catch (FormatException ex)
        {
            context.LogError(FunctionName, $"{FunctionName}: format error: {ex.Message}");
            return FunctionResult<TNode>.Failed(currentNode);
        }
    }

    /// <summary>
    /// Hand string.Format a typed value where the node has one — a format specifier such as
    /// F2 or 00000 is meaningless once the value has been flattened to a string.
    /// </summary>
    private static object? AsFormattable(TNode node, INodeAdapter<TNode> adapter) =>
        adapter.GetNodeKind(node) switch
        {
            NodeKind.Null    => string.Empty,
            NodeKind.Number  => adapter.TryGetDouble(node),
            NodeKind.Boolean => adapter.TryGetBoolean(node),
            _                => adapter.TryGetString(node) ?? string.Empty
        };
}
