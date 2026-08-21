using System.Text.RegularExpressions;

namespace TLio.Extensions.Text;

/// <summary>
/// =regexExtract(str, pattern[, group]) — pulls the first match of a .NET regular expression out
/// of a string. The optional third argument is a capture group index; it defaults to 0, the whole
/// match.
///
/// No match returns an empty string, not a failure. =indexOf sets that precedent by answering -1
/// rather than failing: an absent match is an answer, and a failure would abort the whole script
/// (docs/behaviour-decisions.md B2) — wrong for an extractor whose result is about to be handed
/// to =isEmpty. A group index the pattern does not have is the opposite case: that is an
/// authoring error, so it logs an error and fails.
///
/// Matching is capped by a one second timeout, the same cap =matches uses, so a catastrophically
/// backtracking pattern cannot hang the host. The pattern is read with TryGetRegexStringArg, so a
/// leading '$' stays regex syntax instead of being re-read as a JSONPath.
/// </summary>
public class RegexExtract<TNode> : TextFunctionBase<TNode>
{
    private static readonly TimeSpan MatchTimeout = TimeSpan.FromSeconds(1);

    public override FunctionResult<TNode> Execute(TNode currentNode, TNode dataContext, IExecutionContext<TNode> context)
    {
        if (Arguments.Count < 2)
        {
            context.LogWarning(FunctionName, $"{FunctionName}: at least two arguments required (str, pattern).");
            return FunctionResult<TNode>.Failed(currentNode);
        }
        if (!TryGetStringArg(Arguments[0], out var str, currentNode, dataContext, context, FunctionName))
            return FunctionResult<TNode>.Failed(currentNode);
        if (!TryGetRegexStringArg(Arguments[1], out var pattern, currentNode, dataContext, context, FunctionName))
            return FunctionResult<TNode>.Failed(currentNode);

        var group = 0;
        if (Arguments.Count >= 3 &&
            !TryGetIntArg(Arguments[2], out group, currentNode, dataContext, context, FunctionName))
            return FunctionResult<TNode>.Failed(currentNode);

        Regex regex;
        try
        {
            regex = new Regex(pattern, RegexOptions.None, MatchTimeout);
        }
        catch (ArgumentException ex)
        {
            context.LogError(FunctionName, $"{FunctionName}: invalid pattern '{pattern}': {ex.Message}");
            return FunctionResult<TNode>.Failed(currentNode);
        }

        // Validated against the pattern rather than against the match, so the authoring error is
        // reported even when the input happens not to match.
        if (!regex.GetGroupNumbers().Contains(group))
        {
            context.LogError(FunctionName,
                $"{FunctionName}: pattern '{pattern}' has no capture group {group}.");
            return FunctionResult<TNode>.Failed(currentNode);
        }

        try
        {
            var match = regex.Match(str);
            var value = match.Success ? match.Groups[group].Value : string.Empty;
            return FunctionResult<TNode>.Successful(context.NodeAdapter.CreateString(value));
        }
        catch (RegexMatchTimeoutException)
        {
            context.LogError(FunctionName, $"{FunctionName}: pattern '{pattern}' timed out.");
            return FunctionResult<TNode>.Failed(currentNode);
        }
    }
}
