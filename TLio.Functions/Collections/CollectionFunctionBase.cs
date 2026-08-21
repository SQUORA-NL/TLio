using TLio.Core.Contracts;
using TLio.Core.Models;

namespace TLio.Functions.Collections;

/// <summary>
/// Base class for the collection functions (<c>distinct</c>, <c>sort</c>, <c>sortby</c>).
///
/// These are the only built-in functions that return a <b>collection</b> rather than a single
/// value, so they share three concerns no other pack has:
///
///   - <see cref="TryResolveElements"/> — turning one argument into the list of elements to work
///     on, whether the path named the array itself (<c>$.items</c>) or its members
///     (<c>$.items[*]</c>).
///   - <see cref="TryReadDirection"/> — the optional <c>'asc'</c> / <c>'desc'</c> argument.
///   - <see cref="Order"/> — the comparison rule shared by <c>sort</c> and <c>sortby</c>.
///
/// Null-handling contract: a path that matches nothing is an error (log + fail), matching Math
/// and Text. An array that exists but is <i>empty</i> is an answer, not an error — it produces
/// an empty array.
/// </summary>
public abstract class CollectionFunctionBase<TNode> : FunctionBase<TNode>
{
    protected const string Ascending = "asc";
    protected const string Descending = "desc";

    // ── Element resolution ────────────────────────────────────────────────────

    /// <summary>
    /// Resolve one argument into the elements to operate on.
    ///
    /// A single matched node that is an array contributes its elements, so <c>$.items</c> and
    /// <c>$.items[*]</c> both mean "the members of items". A single matched node that is not an
    /// array contributes itself, giving a one-element list — the same flattening the Math pack
    /// applies to a scalar. Several matched nodes are already the elements and are taken as they
    /// come, so an array of arrays survives <c>$.items</c> intact.
    ///
    /// Returns false, having logged an error, when the path matched nothing.
    /// </summary>
    protected static bool TryResolveElements(
        IFunctionSupportedValue<TNode> arg,
        out List<TNode> elements,
        TNode currentNode, TNode dataContext,
        IExecutionContext<TNode> context, string funcName)
    {
        elements = new List<TNode>();

        var result = ResolveArg(arg, currentNode, dataContext, context);
        if (!result.Success || result.Data.Count == 0)
        {
            context.LogError(funcName, $"{funcName}: argument path not found.");
            return false;
        }

        if (result.Data.Count == 1 && context.NodeAdapter.IsArray(result.Data[0]))
        {
            elements.AddRange(context.NodeAdapter.GetArrayElements(result.Data[0]));
            return true;
        }

        elements.AddRange(result.Data);
        return true;
    }

    /// <summary>Build a fresh array node holding deep clones of <paramref name="elements"/>.</summary>
    protected static TNode BuildArray(IEnumerable<TNode> elements, INodeAdapter<TNode> adapter)
    {
        var array = adapter.CreateArray();
        foreach (var element in elements)
            adapter.AppendToArray(array, adapter.DeepClone(element));
        return array;
    }

    // ── Direction ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Read the optional direction argument. Accepts <c>asc</c> and <c>desc</c>,
    /// case-insensitively; anything else is an error naming the accepted values.
    /// </summary>
    protected static bool TryReadDirection(
        IFunctionSupportedValue<TNode> arg,
        out bool descending,
        TNode currentNode, TNode dataContext,
        IExecutionContext<TNode> context, string funcName)
    {
        descending = false;

        var result = ResolveArg(arg, currentNode, dataContext, context);
        if (!result.Success || result.Data.Count == 0)
        {
            context.LogError(funcName, $"{funcName}: direction argument path not found.");
            return false;
        }

        var text = context.NodeAdapter.TryGetString(result.Data[0])?.Trim();
        if (string.Equals(text, Ascending, StringComparison.OrdinalIgnoreCase)) return true;
        if (string.Equals(text, Descending, StringComparison.OrdinalIgnoreCase))
        {
            descending = true;
            return true;
        }

        context.LogError(funcName,
            $"{funcName}: direction '{text}' is not recognised — accepted values are '{Ascending}' and '{Descending}'.");
        return false;
    }

    // ── Ordering ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Order <paramref name="items"/> by <paramref name="keys"/> (same length, same order),
    /// numerically when <b>every</b> key is numeric and by <see cref="StringComparer.Ordinal"/>
    /// otherwise. A single non-numeric key therefore switches the whole collection to text
    /// ordering — deliberately, so a mixed array never produces a numeric-looking order that
    /// only holds for part of it.
    ///
    /// <c>OrderBy</c> is used rather than <c>List.Sort</c> because it is stable: elements with
    /// equal keys keep their document order.
    /// </summary>
    protected static List<T> Order<T>(
        IReadOnlyList<T> items,
        IReadOnlyList<TNode> keys,
        bool descending,
        INodeAdapter<TNode> adapter)
    {
        IEnumerable<int> indexes = Enumerable.Range(0, items.Count);

        if (keys.Count > 0 && keys.All(k => IsNumeric(k, adapter)))
        {
            var numbers = keys.Select(k => adapter.TryGetDouble(k)!.Value).ToList();
            indexes = descending
                ? indexes.OrderByDescending(i => numbers[i])
                : indexes.OrderBy(i => numbers[i]);
        }
        else
        {
            var texts = keys.Select(k => TextKey(k, adapter)).ToList();
            indexes = descending
                ? indexes.OrderByDescending(i => texts[i], StringComparer.Ordinal)
                : indexes.OrderBy(i => texts[i], StringComparer.Ordinal);
        }

        return indexes.Select(i => items[i]).ToList();
    }

    /// <summary>
    /// A node counts as numeric when it reads as a number and does not read as a boolean.
    /// The boolean exclusion matters for the untyped formats, where <c>&lt;flag&gt;true&lt;/flag&gt;</c>
    /// is text that an adapter might otherwise be willing to coerce.
    /// </summary>
    private static bool IsNumeric(TNode node, INodeAdapter<TNode> adapter) =>
        !adapter.IsNull(node)
        && adapter.GetNodeKind(node) != NodeKind.Boolean
        && adapter.TryGetDouble(node).HasValue;

    /// <summary>
    /// The text form used for ordinal ordering. Null sorts as the empty string; a container has
    /// no scalar text, so its serialised form stands in and keeps the order deterministic.
    /// </summary>
    private static string TextKey(TNode node, INodeAdapter<TNode> adapter)
    {
        if (adapter.IsNull(node)) return string.Empty;
        return adapter.TryGetString(node) ?? adapter.Serialize(node);
    }

    // ── Per-element path resolution ───────────────────────────────────────────

    /// <summary>
    /// True when <paramref name="keyPath"/> is a plain chain of property names once the format's
    /// root / current-item token has been stripped — the only shape that means the same thing in
    /// every format when resolved against a single element.
    ///
    /// The fetchers do not agree on what a path means when handed a node other than the root:
    /// JSONPath and the YAML dot-notation evaluate <c>$.premium</c> relative to the node they are
    /// given, but XPath always evaluates against the owning <i>document</i> node, so
    /// <c>/premium</c> would resolve somewhere else entirely. Walking the chain with
    /// <see cref="INodeAdapter{TNode}.GetProperty"/> sidesteps that difference, which is why a
    /// subscript, wildcard, predicate or recursive descent is rejected outright rather than
    /// quietly handed to a fetcher that would answer differently per format.
    /// </summary>
    protected static bool TrySplitKeyPath(string keyPath, IItemsFetcher<TNode> fetcher, out string[] segments)
    {
        segments = Array.Empty<string>();

        var relative = keyPath.Trim();
        if (relative.StartsWith(fetcher.RootPathIndicator, StringComparison.Ordinal))
            relative = relative[fetcher.RootPathIndicator.Length..];
        else if (relative.StartsWith(fetcher.CurrentItemPathIndicator, StringComparison.Ordinal))
            relative = relative[fetcher.CurrentItemPathIndicator.Length..];

        // A delimiter still leading here separates the root token from the first name
        // ("$" + "." + "premium"). When the format spells root and delimiter the same way
        // ("/" in XPath) there is nothing left to strip, and a second slash means "at any
        // depth" — which is exactly the shape this method refuses.
        if (fetcher.RootPathIndicator != fetcher.PathDelimiter &&
            relative.StartsWith(fetcher.PathDelimiter, StringComparison.Ordinal))
            relative = relative[fetcher.PathDelimiter.Length..];

        if (relative.Length == 0) return false;

        var parts = relative.Split(fetcher.PathDelimiter);
        if (!parts.All(IsPlainName)) return false;

        segments = parts;
        return true;
    }

    /// <summary>
    /// Walk a property chain from <paramref name="element"/>. Returns false when any step is
    /// missing — that is an answer about this element, not an error about the script.
    /// </summary>
    protected static bool TryWalk(TNode element, string[] segments, INodeAdapter<TNode> adapter, out TNode value)
    {
        var current = element;
        foreach (var segment in segments)
        {
            var next = adapter.GetProperty(current, segment);
            if (next is null)
            {
                value = default!;
                return false;
            }
            current = next;
        }
        value = current;
        return true;
    }

    /// <summary>A segment naming a property outright — no subscript, wildcard or predicate.</summary>
    private static bool IsPlainName(string segment) =>
        segment.Length > 0 &&
        segment.All(c => c is not ('[' or ']' or '*' or '(' or ')' or '@' or '?' or '$'));
}
