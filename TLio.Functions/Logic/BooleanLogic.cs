using TLio.Core.Contracts;
using TLio.Core.Models;

namespace TLio.Functions.Logic;

/// <summary>
/// =and(a, b, ...) — true when every argument is true. Evaluates left to right and stops
/// at the first false argument.
/// </summary>
public class AndFunction<TNode> : PredicateFunctionBase<TNode>
{
    public override string FunctionName => "and";

    public override FunctionResult<TNode> Execute(TNode currentNode, TNode dataContext, IExecutionContext<TNode> context)
    {
        if (!RequireArguments(1, currentNode, context, "at least one argument required", out var failure))
            return failure;

        foreach (var arg in Arguments)
            if (!ResolveTruthy(arg, currentNode, dataContext, context))
                return Boolean(false, context);

        return Boolean(true, context);
    }
}

/// <summary>
/// =or(a, b, ...) — true when any argument is true. Evaluates left to right and stops
/// at the first true argument.
/// </summary>
public class OrFunction<TNode> : PredicateFunctionBase<TNode>
{
    public override string FunctionName => "or";

    public override FunctionResult<TNode> Execute(TNode currentNode, TNode dataContext, IExecutionContext<TNode> context)
    {
        if (!RequireArguments(1, currentNode, context, "at least one argument required", out var failure))
            return failure;

        foreach (var arg in Arguments)
            if (ResolveTruthy(arg, currentNode, dataContext, context))
                return Boolean(true, context);

        return Boolean(false, context);
    }
}

/// <summary>=not(value) — the negation of the argument's truthiness.</summary>
public class NotFunction<TNode> : PredicateFunctionBase<TNode>
{
    public override string FunctionName => "not";

    public override FunctionResult<TNode> Execute(TNode currentNode, TNode dataContext, IExecutionContext<TNode> context)
    {
        if (!RequireArguments(1, currentNode, context, "one argument required", out var failure))
            return failure;

        return Boolean(!ResolveTruthy(Arguments[0], currentNode, dataContext, context), context);
    }
}
