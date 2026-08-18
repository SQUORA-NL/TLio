using TLio.Core.Contracts;
using TLio.Core.Models;

namespace TLio.Functions.Logic;

/// <summary>
/// =equals(a, b) — true when both values are equal.
/// Types are bridged: the number 1 equals the text "1", and two objects compare structurally.
/// A path that matches nothing counts as absent, so it equals only another absent/null value.
/// </summary>
public class EqualsFunction<TNode> : PredicateFunctionBase<TNode>
{
    public override string FunctionName => "equals";

    protected virtual bool Invert => false;

    public override FunctionResult<TNode> Execute(TNode currentNode, TNode dataContext, IExecutionContext<TNode> context)
    {
        if (!RequireArguments(2, currentNode, context, "two arguments required (left, right)", out var failure))
            return failure;

        var leftFound = TryResolveNode(Arguments[0], currentNode, dataContext, context, out var left);
        var rightFound = TryResolveNode(Arguments[1], currentNode, dataContext, context, out var right);

        // An absent path behaves like null: absent equals absent, absent equals null,
        // and absent differs from any present value.
        var equal = (leftFound, rightFound) switch
        {
            (false, false) => true,
            (false, true)  => context.NodeAdapter.IsNull(right),
            (true, false)  => context.NodeAdapter.IsNull(left),
            _              => NodeComparison.AreEqual(left, right, context.NodeAdapter)
        };

        return Boolean(Invert ? !equal : equal, context);
    }
}

/// <summary>=notEquals(a, b) — the negation of =equals(a, b).</summary>
public class NotEqualsFunction<TNode> : EqualsFunction<TNode>
{
    public override string FunctionName => "notEquals";
    protected override bool Invert => true;
}
