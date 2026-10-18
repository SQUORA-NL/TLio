namespace TLio.Functions.Schema;

/// <summary>What a schema says about properties it does not list.</summary>
public enum AdditionalMode
{
    /// <summary>No <c>additionalProperties</c> anywhere in the view.</summary>
    Unspecified,
    /// <summary><c>additionalProperties: false</c> and nothing allows them.</summary>
    Forbidden,
    /// <summary><c>true</c>, or a schema — any branch allowing them wins (union).</summary>
    Allowed
}

/// <summary>
/// "This place in the document is described by these schemas." A view is a list because one
/// place can be described several times at once — through <c>allOf</c>, through every branch of a
/// <c>oneOf</c>, through a <c>$ref</c> plus sibling keywords, or because two branches both declare
/// the same property. <see cref="Flatten"/> turns the list into one answer.
/// </summary>
public sealed class SchemaView<TNode>
{
    private readonly SchemaDocument<TNode> _document;
    private readonly IBranchSelector<TNode> _selector;

    public SchemaView(
        SchemaDocument<TNode> document,
        IReadOnlyList<SchemaNode<TNode>> seeds,
        IBranchSelector<TNode> selector)
    {
        _document = document;
        Seeds = seeds;
        _selector = selector;
    }

    public IReadOnlyList<SchemaNode<TNode>> Seeds { get; }

    /// <summary>
    /// Expand references and composition into the effective shape at this place.
    ///
    /// Expansion order is the order that decides property order: the schema itself, then its
    /// <c>$ref</c> target, then each <c>allOf</c> member, then the branches the selector keeps.
    /// A property keeps the position of its <b>first</b> declaration; later declarations only add
    /// to what is known about it. A schema reached twice (a diamond, or a reference cycle) is
    /// expanded once.
    /// </summary>
    /// <param name="data">
    /// The document node this view describes. Phase 1 selectors ignore it; a discriminator
    /// selector reads it to choose a branch.
    /// </param>
    public FlatSchema<TNode> Flatten(TNode? data)
    {
        var visited = new HashSet<SchemaNode<TNode>>(ReferenceEqualityComparer.Instance);
        var expanded = new List<SchemaNode<TNode>>();
        foreach (var seed in Seeds) Expand(seed, data, visited, expanded);
        return new FlatSchema<TNode>(_document, _selector, expanded);
    }

    private void Expand(
        SchemaNode<TNode> node, TNode? data,
        HashSet<SchemaNode<TNode>> visited, List<SchemaNode<TNode>> expanded)
    {
        if (!visited.Add(node)) return;
        expanded.Add(node);

        if (node.Ref is not null && _document.TryResolveRef(node.Ref, out var target))
            Expand(target, data, visited, expanded);

        foreach (var member in node.AllOf) Expand(member, data, visited, expanded);

        ExpandBranches(BranchKind.OneOf, node.OneOf, data, visited, expanded);
        ExpandBranches(BranchKind.AnyOf, node.AnyOf, data, visited, expanded);
        ExpandBranches(BranchKind.Conditional, node.Conditional, data, visited, expanded);
    }

    private void ExpandBranches(
        BranchKind kind, IReadOnlyList<SchemaNode<TNode>> alternatives, TNode? data,
        HashSet<SchemaNode<TNode>> visited, List<SchemaNode<TNode>> expanded)
    {
        if (alternatives.Count == 0) return;
        foreach (var branch in _selector.Select(kind, alternatives, data))
            Expand(branch, data, visited, expanded);
    }
}

/// <summary>The merged, effective shape of one place in the document.</summary>
public sealed class FlatSchema<TNode>
{
    internal FlatSchema(
        SchemaDocument<TNode> document,
        IBranchSelector<TNode> selector,
        IReadOnlyList<SchemaNode<TNode>> nodes)
    {
        Nodes = nodes;

        var order = new List<string>();
        var byName = new Dictionary<string, List<SchemaNode<TNode>>>(StringComparer.Ordinal);
        var items = new List<SchemaNode<TNode>>();
        var prefix = new List<List<SchemaNode<TNode>>>();
        var additional = new List<SchemaNode<TNode>>();
        var allowed = false;
        var forbidden = false;

        foreach (var node in nodes)
        {
            if (node.HasProperties) HasProperties = true;
            foreach (var (name, child) in node.Properties)
            {
                if (!byName.TryGetValue(name, out var list))
                {
                    byName[name] = list = new List<SchemaNode<TNode>>();
                    order.Add(name);
                }
                list.Add(child);
            }

            if (node.Items is not null) items.Add(node.Items);
            for (var i = 0; i < node.PrefixItems.Count; i++)
            {
                while (prefix.Count <= i) prefix.Add(new List<SchemaNode<TNode>>());
                prefix[i].Add(node.PrefixItems[i]);
            }

            if (node.AdditionalAllowed == true) allowed = true;
            if (node.AdditionalAllowed == false) forbidden = true;
            if (node.AdditionalSchema is not null) additional.Add(node.AdditionalSchema);

            Type ??= node.Type;
            if (!HasDefault && node.HasDefault)
            {
                HasDefault = true;
                Default = node.Default;
            }
            if (!HasConst && node.AllowedValues.Count == 1)
            {
                HasConst = true;
                Const = node.AllowedValues[0];
            }
        }

        Properties = order
            .Select(name => new KeyValuePair<string, SchemaView<TNode>>(
                name, new SchemaView<TNode>(document, byName[name], selector)))
            .ToList();

        if (items.Count > 0) Items = new SchemaView<TNode>(document, items, selector);
        PrefixItems = prefix.Select(p => new SchemaView<TNode>(document, p, selector)).ToList();
        if (additional.Count > 0) AdditionalView = new SchemaView<TNode>(document, additional, selector);
        Additional = allowed ? AdditionalMode.Allowed : forbidden ? AdditionalMode.Forbidden : AdditionalMode.Unspecified;
    }

    /// <summary>Every schema that contributed, in expansion order.</summary>
    public IReadOnlyList<SchemaNode<TNode>> Nodes { get; }

    public bool HasProperties { get; }

    /// <summary>Properties in the order the schema declares them (first declaration wins).</summary>
    public IReadOnlyList<KeyValuePair<string, SchemaView<TNode>>> Properties { get; }

    public SchemaView<TNode>? Items { get; }
    public IReadOnlyList<SchemaView<TNode>> PrefixItems { get; }

    public AdditionalMode Additional { get; }
    public SchemaView<TNode>? AdditionalView { get; }

    public string? Type { get; }
    public bool HasDefault { get; }
    public TNode? Default { get; }
    public bool HasConst { get; }
    public TNode? Const { get; }

    /// <summary>
    /// True when the schema says something about the shape of an object or array here. A schema
    /// that does not — <c>{}</c>, or just <c>{"type":"object"}</c> — is <i>opaque</i>: it allows
    /// whatever is there, and filtering must not strip a free-form value for lack of a description.
    /// </summary>
    public bool HasStructure => HasProperties || Items is not null || PrefixItems.Count > 0;
}
