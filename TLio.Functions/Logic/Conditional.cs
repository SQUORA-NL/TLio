using TLio.Core.Contracts;
using TLio.Core.Models;

namespace TLio.Functions.Logic;

/// <summary>
/// =if(condition, whenTrue, whenFalse) — the conditional *value*. Evaluates the condition and
/// returns one of the two branches, so a field that is produced two ways costs one expression
/// instead of an <c>ifElse</c> block wrapped around a single <c>put</c>.
///
/// Only the chosen branch is evaluated. That laziness is the point: it is what makes
/// <c>=if(=exists($.a),=fetch($.a),'-')</c> safe, because <c>fetch</c> on a missing path fails
/// the script and must therefore never run when the condition said it would not resolve.
///
/// Three arguments, never two. A two-argument form returning null when false was deliberately
/// left out — null is load-bearing in this library (a null slot is a container a later command
/// can write into, see docs/behaviour-decisions.md), so handing one out implicitly is the wrong
/// default. Write the false branch.
///
/// The condition goes through <see cref="PredicateFunctionBase{TNode}.ResolveTruthy"/>, which is
/// why this extends the predicate base even though what it returns is a value: a condition path
/// matching nothing is an answer (false), not a failure.
/// </summary>
public class IfFunction<TNode> : PredicateFunctionBase<TNode>
{
    public override string FunctionName => "if";

    public override FunctionResult<TNode> Execute(TNode currentNode, TNode dataContext, IExecutionContext<TNode> context)
    {
        if (!RequireArguments(3, currentNode, context,
                "three arguments required (condition, whenTrue, whenFalse)", out var failure))
            return failure;

        var branch = ResolveTruthy(Arguments[0], currentNode, dataContext, context)
            ? Arguments[1]
            : Arguments[2];

        // Only the selected argument is touched — resolving both branches here would defeat
        // the whole reason this function exists.
        return ResolveArg(branch, currentNode, dataContext, context);
    }
}

/// <summary>
/// =coalesce(a, b, ...) — returns the first argument that resolves to a node that is neither
/// null nor an empty string. Variadic, evaluated left to right, stopping at the first winner.
///
/// A path that matches nothing is skipped rather than failing, which is what makes the function
/// useful for the case it was written for: SIVI/AFD mapping regularly has three candidate
/// sources for one field, and today that is nested <c>fetch(path, fetch(path, ...))</c>.
/// <c>fetch(path, default)</c> still covers the single-fallback case.
///
/// When nothing qualifies the function fails, which aborts the script (docs/behaviour-decisions.md
/// B2). That is deliberate: "none of these sources had a value" is a mapping error, not an empty
/// answer. End the argument list with a literal default when the absent case is legitimate.
///
/// An empty object or an empty array qualifies — only null and the empty string are skipped.
/// Use <c>=isEmpty(...)</c> when empty collections should count as absent too.
/// </summary>
public class CoalesceFunction<TNode> : PredicateFunctionBase<TNode>
{
    public override string FunctionName => "coalesce";

    public override FunctionResult<TNode> Execute(TNode currentNode, TNode dataContext, IExecutionContext<TNode> context)
    {
        if (!RequireArguments(1, currentNode, context, "at least one argument required", out var failure))
            return failure;

        var adapter = context.NodeAdapter;

        foreach (var arg in Arguments)
        {
            if (!TryResolveNode(arg, currentNode, dataContext, context, out var node))
                continue;

            if (adapter.IsNull(node))
                continue;

            if (!adapter.IsObject(node) && !adapter.IsArray(node) &&
                string.IsNullOrEmpty(adapter.TryGetString(node)))
                continue;

            return FunctionResult<TNode>.Successful(node);
        }

        context.LogError(FunctionName,
            $"{FunctionName}: no argument produced a value — add a literal default as the last argument.");
        return FunctionResult<TNode>.Failed(currentNode);
    }
}
