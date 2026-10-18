using TLio.Core.Contracts;

namespace TLio.Functions.Schema;

/// <summary>
/// Picks the branch of a tagged union the data belongs to, without validating anything.
///
/// A tagged union is written as branches that each pin one property to a constant:
/// <c>{"oneOf":[{"properties":{"kind":{"const":"car"},…}},{"properties":{"kind":{"const":"plane"},…}}]}</c>.
/// A <b>discriminator</b> is a property pinned (<c>const</c>, or <c>enum</c>) in <i>every</i> branch.
/// When the data carries a value for it that exactly one branch allows, that branch is the answer.
///
/// Anything less certain falls back to the union, which is what the default selector does and the
/// only answer that cannot lose a property:
/// <list type="bullet">
///   <item>the data is not an object, or lacks the property;</item>
///   <item>no property is pinned in every branch (a branch with no discriminator could be any);</item>
///   <item>no branch, or more than one, allows the value — the next candidate property is tried
///     first, so a union discriminated by <c>kind</c> <i>and</i> <c>version</c> still resolves.</item>
/// </list>
///
/// Only <c>oneOf</c> and <c>anyOf</c> are discriminated. <c>then</c>/<c>else</c> depend on an
/// <c>if</c> this never evaluates, so they stay a union.
///
/// Instances are per call: the selector holds the schema document it reads branches through.
/// </summary>
public sealed class DiscriminatorBranchSelector<TNode> : IBranchSelector<TNode>
{
    private readonly SchemaDocument<TNode> _document;
    private readonly INodeAdapter<TNode> _adapter;

    public DiscriminatorBranchSelector(SchemaDocument<TNode> document, INodeAdapter<TNode> adapter)
    {
        _document = document;
        _adapter = adapter;
    }

    public IReadOnlyList<SchemaNode<TNode>> Select(
        BranchKind kind, IReadOnlyList<SchemaNode<TNode>> alternatives, TNode? data)
    {
        if (kind == BranchKind.Conditional || alternatives.Count < 2) return alternatives;
        if (data is null || !_adapter.IsObject(data)) return alternatives;

        // What each branch pins, read through the plain union so choosing here never recurses
        // into another discriminated choice.
        var pins = alternatives.Select(Pins).ToList();

        // Candidates: pinned in every branch, in the order the first branch declares them.
        var candidates = pins[0].Keys.Where(name => pins.All(p => p.ContainsKey(name)));

        foreach (var name in candidates)
        {
            var value = _adapter.GetProperty(data, name);
            if (value is null || _adapter.IsObject(value) || _adapter.IsArray(value)) continue;

            var matching = new List<SchemaNode<TNode>>();
            for (var i = 0; i < alternatives.Count; i++)
                if (pins[i][name].Any(allowed => SameValue(allowed, value)))
                    matching.Add(alternatives[i]);

            if (matching.Count == 1) return matching;
        }

        return alternatives;
    }

    /// <summary>property name → the values this branch allows it to take (only pinned properties).</summary>
    private Dictionary<string, IReadOnlyList<TNode>> Pins(SchemaNode<TNode> branch)
    {
        var flat = new SchemaView<TNode>(_document, new[] { branch }, UnionBranchSelector<TNode>.Instance)
            .Flatten(default);

        var pins = new Dictionary<string, IReadOnlyList<TNode>>(StringComparer.Ordinal);
        foreach (var (name, view) in flat.Properties)
        {
            var allowed = view.Flatten(default).AllowedValues;
            if (allowed.Count > 0) pins[name] = allowed;
        }
        return pins;
    }

    /// <summary>
    /// Equality of a schema constant and a data value across formats: XML and YAML scalars are
    /// untyped, so <c>1</c>, <c>"1"</c> and <c>1.0</c> must agree, and so must <c>true</c> and
    /// <c>"true"</c>.
    /// </summary>
    private bool SameValue(TNode allowed, TNode actual)
    {
        if (_adapter.IsNull(allowed) || _adapter.IsNull(actual))
            return _adapter.IsNull(allowed) && _adapter.IsNull(actual);

        var a = _adapter.TryGetBoolean(allowed);
        var b = _adapter.TryGetBoolean(actual);
        if (a.HasValue && b.HasValue) return a.Value == b.Value;

        var x = _adapter.TryGetDouble(allowed);
        var y = _adapter.TryGetDouble(actual);
        if (x.HasValue && y.HasValue) return x.Value == y.Value;

        return string.Equals(_adapter.TryGetString(allowed), _adapter.TryGetString(actual), StringComparison.Ordinal);
    }
}
