using TLio.Core;
using TLio.Core.Contracts;

namespace TLio.Extensions.Looping;

/// <summary>
/// Shared bits for <see cref="ForEach{TNode}"/> and <see cref="While{TNode}"/>.
///
/// The current loop item is <see cref="IExecutionContext{TNode}.CurrentNode"/> — an ordinary
/// property every command already has access to, set by <c>forEach</c> around each iteration
/// and left alone by <c>while</c> (a while body has no "current item," only whatever absolute
/// state its own commands read and write). A nested command reaches it with the *same* <c>@</c>
/// token every relative path in TLio already uses (<c>path: "@.status"</c>, bare
/// <c>path: "@"</c> for the whole item, <c>value: "@.name"</c>) — see
/// <c>TLio.Commands.Logic.PropertyChangeCommand.Execute</c> for where that resolution happens.
/// This package adds no new addressing syntax, just a place for <c>@</c> to point during a loop.
/// </summary>
internal static class LoopSupport
{
    public const int DefaultMaxIterations = 100_000;

    /// <summary>
    /// Resolves a loop command's own <c>path</c> to absolute: first <c>@</c>, relative to
    /// <see cref="IExecutionContext{TNode}.CurrentNode"/> (an enclosing <c>forEach</c>'s current
    /// element — this is what makes <c>{"command":"forEach","path":"@.members",...}</c> nested
    /// inside another <c>forEach</c> mean "this element's members"), then any
    /// <c>=indirect(...)</c> expression.
    /// </summary>
    public static string? ResolvePath<TNode>(
        string path, TNode dataContext, IExecutionContext<TNode> context, string commandName)
    {
        var withCurrentResolved = path;
        if (context.CurrentNode != null &&
            path.StartsWith(context.ItemsFetcher.CurrentItemPathIndicator, StringComparison.Ordinal))
        {
            withCurrentResolved = context.ItemsFetcher.ResolveRelativePath(path, context.CurrentNode, dataContext);
        }

        var resolved = context.ItemsFetcher.ProcessIndirectPath(withCurrentResolved, dataContext);
        if (resolved != null)
            return resolved;

        context.LogWarning(CoreConstants.CommandExecution,
            $"{commandName}: could not resolve path '{path}' — nothing changed");
        return null;
    }
}
