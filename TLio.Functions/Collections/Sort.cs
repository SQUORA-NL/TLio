using TLio.Core.Contracts;
using TLio.Core.Models;

namespace TLio.Functions.Collections;

/// <summary>
/// =sort($.array, 'asc'|'desc') — returns a new array with the elements ordered. The direction
/// is optional and defaults to <c>'asc'</c>; it is read case-insensitively, and any other word
/// is an error rather than a silent ascending sort.
///
/// Ordering a collection has no other expression in the language — <c>min</c>, <c>max</c>,
/// <c>minDate</c> and <c>maxDate</c> answer the one-value question and stop there.
///
/// The comparison rule is worth reading before assuming: elements are compared <b>numerically
/// only when every one of them is numeric</b>, and by their string form using
/// <see cref="StringComparer.Ordinal"/> otherwise. A single non-numeric element therefore moves
/// the whole array to text ordering, where "10" sorts before "9" — a mixed array must not
/// produce a numeric-looking order that holds for only part of it.
///
/// The sort is stable, so elements that compare equal keep their document order, and the input
/// array is never touched — the result is a fresh array of deep clones.
/// </summary>
public class Sort<TNode> : CollectionFunctionBase<TNode>
{
    public override string FunctionName => "sort";

    public override FunctionResult<TNode> Execute(TNode currentNode, TNode dataContext, IExecutionContext<TNode> context)
    {
        if (Arguments.Count is < 1 or > 2)
        {
            context.LogError(FunctionName,
                $"{FunctionName}() requires an array argument and an optional direction ('{Ascending}' or '{Descending}').");
            return FunctionResult<TNode>.Failed(currentNode);
        }

        if (!TryResolveElements(Arguments[0], out var elements, currentNode, dataContext, context, FunctionName))
            return FunctionResult<TNode>.Failed(currentNode);

        var descending = false;
        if (Arguments.Count == 2 &&
            !TryReadDirection(Arguments[1], out descending, currentNode, dataContext, context, FunctionName))
            return FunctionResult<TNode>.Failed(currentNode);

        var ordered = Order(elements, elements, descending, context.NodeAdapter);
        return FunctionResult<TNode>.Successful(BuildArray(ordered, context.NodeAdapter));
    }
}
