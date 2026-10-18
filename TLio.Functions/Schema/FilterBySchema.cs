using TLio.Core.Contracts;
using TLio.Core.Models;

namespace TLio.Functions.Schema;

/// <summary>
/// =filterBySchema(schema, data [, 'keepAdditional']) — returns a copy of the data holding only
/// what the schema describes.
///
/// Ported from JLio's <c>filterBySchema</c>. By default anything the schema does not list is
/// dropped, as in JLio. Two deliberate refinements:
///
///   - A value whose schema is <i>opaque</i> (<c>{}</c>, or only a <c>type</c>) is kept whole.
///     JLio compares paths, so it strips the contents of a free-form <c>{"type":"object"}</c>;
///     here, "the schema says nothing" does not mean "remove it".
///   - <c>'keepAdditional'</c> honours <c>additionalProperties</c>: unlisted properties survive
///     unless it is <c>false</c>, and an <c>additionalProperties</c> schema is applied to them.
///
/// Composition is a union, as in <c>orderBySchema</c>: a property described by any <c>allOf</c>
/// member or any <c>oneOf</c>/<c>anyOf</c> branch is kept. Picking one branch without validating
/// would silently drop data; keeping the union cannot.
/// </summary>
public class FilterBySchema<TNode> : SchemaFunctionBase<TNode>
{
    private const string KeepAdditional = "keepAdditional";

    public override string FunctionName => "filterBySchema";

    public override FunctionResult<TNode> Execute(TNode currentNode, TNode dataContext, IExecutionContext<TNode> context)
    {
        if (Arguments.Count is < 2 or > 3)
        {
            context.LogError(FunctionName,
                $"{FunctionName}() requires a schema (a path or JSON text), the data path to filter and an optional '{KeepAdditional}'.");
            return FunctionResult<TNode>.Failed(currentNode);
        }

        if (!TryReadSchema(Arguments[0], currentNode, dataContext, context, out var document))
            return FunctionResult<TNode>.Failed(currentNode);

        // The trailing arguments are the data path and the 'keepAdditional' flag, in either order.
        var keepAdditional = false;
        IFunctionSupportedValue<TNode>? dataArg = null;
        for (var i = 1; i < Arguments.Count; i++)
        {
            var raw = Arguments[i].GetValue(currentNode, dataContext, context);
            var text = raw.Success && raw.Data.Count == 1 ? context.NodeAdapter.TryGetString(raw.Data[0]) : null;

            if (string.Equals(text, KeepAdditional, StringComparison.OrdinalIgnoreCase))
                keepAdditional = true;
            else if (dataArg is null)
                dataArg = Arguments[i];
            else
            {
                context.LogError(FunctionName, $"{FunctionName}(): only one data path is accepted.");
                return FunctionResult<TNode>.Failed(currentNode);
            }
        }

        if (dataArg is null)
        {
            context.LogError(FunctionName, $"{FunctionName}(): the data path to filter is missing.");
            return FunctionResult<TNode>.Failed(currentNode);
        }

        if (!TryReadData(dataArg, currentNode, dataContext, context, out var data))
            return FunctionResult<TNode>.Failed(currentNode);

        var adapter = context.NodeAdapter;
        var result = adapter.DeepClone(data);
        var view = new SchemaView<TNode>(document, new[] { document.Root }, UnionBranchSelector<TNode>.Instance);
        SchemaShaper.Filter(result, view, keepAdditional, adapter);

        return FunctionResult<TNode>.Successful(result);
    }
}
