using TLio.Core.Contracts;

namespace TLio.Functions.Schema;

/// <summary>
/// The three things a schema's <i>shape</i> can do to a document: put its properties in order,
/// strip what it does not describe, and build an empty document that has its shape.
///
/// All three work purely through <see cref="INodeAdapter{TNode}"/>, so they behave the same on
/// JSON, XML and YAML. <see cref="Order"/> and <see cref="Filter"/> mutate the node they are
/// given — callers hand them a clone.
/// </summary>
public static class SchemaShaper
{
    // ── Order ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Reorder every object under <paramref name="node"/> to the property order the schema
    /// declares. Properties the schema does not mention are kept, after the declared ones, in
    /// their original relative order. Nothing is added, removed or changed — only reordered.
    /// </summary>
    public static void Order<TNode>(TNode node, SchemaView<TNode> view, INodeAdapter<TNode> adapter)
    {
        var flat = view.Flatten(node);

        if (adapter.IsObject(node))
        {
            var present = adapter.GetPropertyNames(node).ToList();
            var declared = flat.Properties.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);

            foreach (var name in present)
            {
                var child = adapter.GetProperty(node, name);
                if (child is null) continue;

                if (declared.TryGetValue(name, out var childView))
                    Order(child, childView, adapter);
                else if (flat.AdditionalView is not null)
                    Order(child, flat.AdditionalView, adapter);
            }

            var desired = flat.Properties.Select(p => p.Key).Where(present.Contains)
                .Concat(present.Where(n => !declared.ContainsKey(n)))
                .ToList();

            if (!desired.SequenceEqual(present, StringComparer.Ordinal))
                Rewrite(node, desired, adapter);
        }
        else if (adapter.IsArray(node))
        {
            var index = 0;
            foreach (var element in adapter.GetArrayElements(node).ToList())
            {
                var elementView = ElementView(flat, index++);
                if (elementView is not null) Order(element, elementView, adapter);
            }
        }
    }

    /// <summary>
    /// Put the properties back in <paramref name="desired"/> order. There is no "move property"
    /// on the adapter and none is needed: a property that is removed and set again lands last in
    /// every format (JSON object, YAML mapping, XML child element), so doing that in order leaves
    /// them in order. Values are cloned first so no adapter has to cope with re-attaching a node
    /// it has just detached.
    /// </summary>
    private static void Rewrite<TNode>(TNode node, List<string> desired, INodeAdapter<TNode> adapter)
    {
        var values = desired
            .Select(name => (name, value: adapter.DeepClone(adapter.GetProperty(node, name)!)))
            .ToList();

        foreach (var name in desired) adapter.RemoveProperty(node, name);
        foreach (var (name, value) in values) adapter.SetProperty(node, name, value);
    }

    // ── Filter ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Remove, under <paramref name="node"/>, every property the schema does not describe.
    ///
    /// Only places where the schema <i>says something about the shape</i> are filtered: an object
    /// described by <c>properties</c>, an array described by <c>items</c>. A value whose schema is
    /// opaque (<c>{}</c>, or just a <c>type</c>) is kept whole — free-form content is not
    /// "undescribed".
    ///
    /// With <paramref name="keepAdditional"/>, properties the schema does not list survive unless
    /// it says <c>additionalProperties: false</c>; an <c>additionalProperties</c> schema is applied
    /// to them.
    /// </summary>
    public static void Filter<TNode>(
        TNode node, SchemaView<TNode> view, bool keepAdditional, INodeAdapter<TNode> adapter)
    {
        var flat = view.Flatten(node);
        if (!flat.HasStructure) return;

        if (adapter.IsObject(node) && flat.HasProperties)
        {
            var declared = flat.Properties.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);

            foreach (var name in adapter.GetPropertyNames(node).ToList())
            {
                var child = adapter.GetProperty(node, name);
                if (child is null) continue;

                if (declared.TryGetValue(name, out var childView))
                    Filter(child, childView, keepAdditional, adapter);
                else if (keepAdditional && flat.Additional != AdditionalMode.Forbidden)
                {
                    if (flat.AdditionalView is not null)
                        Filter(child, flat.AdditionalView, keepAdditional, adapter);
                }
                else
                    adapter.RemoveProperty(node, name);
            }
        }
        else if (adapter.IsArray(node))
        {
            var index = 0;
            foreach (var element in adapter.GetArrayElements(node).ToList())
            {
                var elementView = ElementView(flat, index++);
                if (elementView is not null) Filter(element, elementView, keepAdditional, adapter);
            }
        }
    }

    private static SchemaView<TNode>? ElementView<TNode>(FlatSchema<TNode> flat, int index) =>
        index < flat.PrefixItems.Count ? flat.PrefixItems[index] : flat.Items;

    // ── Skeleton ──────────────────────────────────────────────────────────────

    private const int MaxSkeletonDepth = 32;

    /// <summary>
    /// Build the empty document a schema describes: an object with every declared property (in
    /// declaration order), an empty array where the schema says array, the schema's own
    /// <c>default</c> (or <c>const</c>) wherever it gives one, and null elsewhere.
    ///
    /// The view should use a selector that commits to one branch
    /// (<see cref="FirstBranchSelector{TNode}"/>): a blank document cannot be two shapes at once.
    /// A recursive schema is cut where it comes back to a schema already being built, so
    /// <c>Node { children: [Node] }</c> terminates — an array is empty, an object property null.
    /// </summary>
    public static TNode Skeleton<TNode>(SchemaView<TNode> view, INodeAdapter<TNode> adapter) =>
        Skeleton(view, adapter, new HashSet<SchemaNode<TNode>>(ReferenceEqualityComparer.Instance), 0);

    private static TNode Skeleton<TNode>(
        SchemaView<TNode> view, INodeAdapter<TNode> adapter,
        HashSet<SchemaNode<TNode>> active, int depth)
    {
        var flat = view.Flatten(default);

        if (flat.HasDefault) return adapter.DeepClone(flat.Default!);
        if (flat.HasConst) return adapter.DeepClone(flat.Const!);

        if (depth >= MaxSkeletonDepth || flat.Nodes.Any(active.Contains))
            return adapter.CreateNull();

        var entered = flat.Nodes.Where(active.Add).ToList();
        try
        {
            if (flat.HasProperties || flat.Type == "object")
            {
                var obj = adapter.CreateObject();
                foreach (var (name, child) in flat.Properties)
                    adapter.SetProperty(obj, name, Skeleton(child, adapter, active, depth + 1));
                return obj;
            }

            if (flat.Type == "array" || flat.Items is not null || flat.PrefixItems.Count > 0)
                return adapter.CreateArray();

            return adapter.CreateNull();
        }
        finally
        {
            foreach (var node in entered) active.Remove(node);
        }
    }
}
