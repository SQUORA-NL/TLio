using TLio.Core.Contracts;
using TLio.Core.Models;

namespace TLio.Functions.Logic;

/// <summary>
/// =exists($.path) — true when the path matches at least one node, whatever its value.
/// A present-but-null property exists; a missing property does not.
/// </summary>
public class ExistsFunction<TNode> : PredicateFunctionBase<TNode>
{
    public override string FunctionName => "exists";

    public override FunctionResult<TNode> Execute(TNode currentNode, TNode dataContext, IExecutionContext<TNode> context)
    {
        if (!RequireArguments(1, currentNode, context, "one argument required (path)", out var failure))
            return failure;

        return Boolean(TryResolveNode(Arguments[0], currentNode, dataContext, context, out _), context);
    }
}

/// <summary>
/// =isNull(value) — true when the value is null, or when the path matches nothing.
/// Use =exists(...) to tell those two cases apart.
/// </summary>
public class IsNullFunction<TNode> : PredicateFunctionBase<TNode>
{
    public override string FunctionName => "isNull";

    public override FunctionResult<TNode> Execute(TNode currentNode, TNode dataContext, IExecutionContext<TNode> context)
    {
        if (!RequireArguments(1, currentNode, context, "one argument required", out var failure))
            return failure;

        if (!TryResolveNode(Arguments[0], currentNode, dataContext, context, out var node))
            return Boolean(true, context);

        return Boolean(context.NodeAdapter.IsNull(node), context);
    }
}

/// <summary>
/// Shared implementation of the type predicates. Reports the document's own type where the
/// format has one (JSON); for untyped formats (XML, YAML) it reports the value's apparent
/// type — see <see cref="NodeKind"/>. A missing path is false for every type check.
/// </summary>
public abstract class TypeCheckFunctionBase<TNode> : PredicateFunctionBase<TNode>
{
    protected abstract NodeKind Expected { get; }

    public override FunctionResult<TNode> Execute(TNode currentNode, TNode dataContext, IExecutionContext<TNode> context)
    {
        if (!RequireArguments(1, currentNode, context, "one argument required", out var failure))
            return failure;

        if (!TryResolveNode(Arguments[0], currentNode, dataContext, context, out var node))
            return Boolean(false, context);

        return Boolean(context.NodeAdapter.GetNodeKind(node) == Expected, context);
    }
}

/// <summary>=isString(value) — true when the value is textual.</summary>
public class IsStringFunction<TNode> : TypeCheckFunctionBase<TNode>
{
    public override string FunctionName => "isString";
    protected override NodeKind Expected => NodeKind.String;
}

/// <summary>=isNumber(value) — true when the value is numeric.</summary>
public class IsNumberFunction<TNode> : TypeCheckFunctionBase<TNode>
{
    public override string FunctionName => "isNumber";
    protected override NodeKind Expected => NodeKind.Number;
}

/// <summary>=isBoolean(value) — true when the value is a boolean.</summary>
public class IsBooleanFunction<TNode> : TypeCheckFunctionBase<TNode>
{
    public override string FunctionName => "isBoolean";
    protected override NodeKind Expected => NodeKind.Boolean;
}

/// <summary>=isArray(value) — true when the value is an array.</summary>
public class IsArrayFunction<TNode> : TypeCheckFunctionBase<TNode>
{
    public override string FunctionName => "isArray";
    protected override NodeKind Expected => NodeKind.Array;
}

/// <summary>=isObject(value) — true when the value is an object.</summary>
public class IsObjectFunction<TNode> : TypeCheckFunctionBase<TNode>
{
    public override string FunctionName => "isObject";
    protected override NodeKind Expected => NodeKind.Object;
}
