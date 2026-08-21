using System.Text.RegularExpressions;

namespace TLio.Extensions.Text;

/// <summary>
/// =regexReplace(str, pattern, replacement) — rewrites every match of a .NET regular expression.
///
/// The replacement uses .NET substitution syntax, so <c>$1</c> is a backreference to the first
/// capture group and <c>$$</c> is a literal dollar sign. The match is unanchored; anchor the
/// pattern yourself with ^ and $ when only a whole-value rewrite should apply.
///
/// Matching is capped by a one second timeout, the same cap =matches uses: a script must not be
/// able to hang the host with a catastrophically backtracking pattern. An invalid pattern and a
/// timeout are both authoring errors — they log an error naming the pattern and fail, which
/// aborts the script.
///
/// The pattern and the replacement are read with TryGetRegexStringArg, so a leading '$' stays
/// regex syntax instead of being re-read as a JSONPath.
/// </summary>
public class RegexReplace<TNode> : TextFunctionBase<TNode>
{
    private static readonly TimeSpan MatchTimeout = TimeSpan.FromSeconds(1);

    public override FunctionResult<TNode> Execute(TNode currentNode, TNode dataContext, IExecutionContext<TNode> context)
    {
        if (Arguments.Count < 3)
        {
            context.LogWarning(FunctionName, $"{FunctionName}: three arguments required (str, pattern, replacement).");
            return FunctionResult<TNode>.Failed(currentNode);
        }
        if (!TryGetStringArg(Arguments[0], out var str, currentNode, dataContext, context, FunctionName))
            return FunctionResult<TNode>.Failed(currentNode);
        if (!TryGetRegexStringArg(Arguments[1], out var pattern, currentNode, dataContext, context, FunctionName))
            return FunctionResult<TNode>.Failed(currentNode);
        if (!TryGetRegexStringArg(Arguments[2], out var replacement, currentNode, dataContext, context, FunctionName))
            return FunctionResult<TNode>.Failed(currentNode);

        try
        {
            var rewritten = Regex.Replace(str, pattern, replacement, RegexOptions.None, MatchTimeout);
            return FunctionResult<TNode>.Successful(context.NodeAdapter.CreateString(rewritten));
        }
        catch (ArgumentException ex)
        {
            context.LogError(FunctionName, $"{FunctionName}: invalid pattern '{pattern}': {ex.Message}");
            return FunctionResult<TNode>.Failed(currentNode);
        }
        catch (RegexMatchTimeoutException)
        {
            context.LogError(FunctionName, $"{FunctionName}: pattern '{pattern}' timed out.");
            return FunctionResult<TNode>.Failed(currentNode);
        }
    }
}
