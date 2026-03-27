using System.Globalization;

namespace TLio.Extensions.Text;

/// <summary>
/// =parse(str) — parses a JSON-formatted string argument into a node.
/// The string must be valid JSON (e.g. "42", "true", "[1,2,3]", "{\"a\":1}").
/// On parse failure returns the input as a string node.
/// </summary>
public class Parse<TNode> : TextFunctionBase<TNode>
{
    public override FunctionResult<TNode> Execute(TNode currentNode, TNode dataContext, IExecutionContext<TNode> context)
    {
        if (Arguments.Count == 0)
        {
            context.LogWarning(FunctionName, $"{FunctionName}: one argument required.");
            return FunctionResult<TNode>.Failed(currentNode);
        }
        if (!TryGetStringArg(Arguments[0], out var str, currentNode, dataContext, context, FunctionName))
            return FunctionResult<TNode>.Failed(currentNode);

        try
        {
            var parsed = context.NodeAdapter.Parse(str);
            return FunctionResult<TNode>.Successful(parsed);
        }
        catch
        {
            // If not parseable as JSON, return as-is string
            return FunctionResult<TNode>.Successful(context.NodeAdapter.CreateString(str));
        }
    }
}
