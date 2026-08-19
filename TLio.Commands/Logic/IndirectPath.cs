using TLio.Core;
using TLio.Core.Contracts;

namespace TLio.Commands.Logic;

/// <summary>
/// Resolves <c>=indirect(...)</c> expressions in a command's path before the path reaches the
/// items fetcher.
///
/// A path is a value like any other: <c>=indirect($.target).field</c> reads the real path out of
/// the document. Every command that takes a path therefore has to resolve it — and every command
/// that forgets crashes, because a raw <c>=</c> is not legal in JsonPath or XPath. Falling back to
/// the unresolved path is equally fatal for the same reason, so an expression that cannot be
/// resolved returns null here and the caller warns and no-ops.
/// </summary>
internal static class IndirectPath
{
    /// <summary>
    /// Returns the resolved path, or null when an <c>=indirect(...)</c> expression could not be
    /// resolved (referenced path missing, or its value is not a non-empty string). Paths with no
    /// indirect expression are returned unchanged.
    /// </summary>
    public static string? TryResolve<TNode>(
        string path, TNode dataContext, IExecutionContext<TNode> context, string commandName,
        string pathLabel = "path")
    {
        var resolved = context.ItemsFetcher.ProcessIndirectPath(path, dataContext);
        if (resolved != null)
            return resolved;

        context.LogWarning(CoreConstants.CommandExecution,
            $"{commandName}: could not resolve the =indirect() expression in {pathLabel} '{path}' — " +
            $"the referenced path must exist and hold a non-empty string path; nothing changed");
        return null;
    }
}
