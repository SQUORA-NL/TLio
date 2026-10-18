namespace TLio.Functions.Schema;

/// <summary>
/// One JSON Schema (sub)schema, reduced to the keywords that decide a document's <b>shape</b>:
/// which properties exist and in what order, what an array holds, and how schemas combine.
///
/// This is not a validator. <c>type</c>, <c>required</c>, <c>pattern</c>, <c>minimum</c> and the
/// rest are never evaluated — <c>orderBySchema</c>, <c>filterBySchema</c> and <c>skeleton</c>
/// only need to know what the schema <i>describes</i>, and that needs no schema library.
///
/// Nodes are plain data, built once per function call by <see cref="SchemaDocument{TNode}"/>.
/// A <c>$ref</c> stays a string here and is followed lazily, which is what lets a recursive
/// schema (a tree whose children are the same tree) exist without an infinite object graph.
/// </summary>
public sealed class SchemaNode<TNode>
{
    /// <summary>True when a <c>properties</c> keyword was present, even if it was empty.</summary>
    public bool HasProperties { get; init; }

    /// <summary>Declared properties in <b>declaration order</b> — the order is the point.</summary>
    public IReadOnlyList<KeyValuePair<string, SchemaNode<TNode>>> Properties { get; init; } =
        Array.Empty<KeyValuePair<string, SchemaNode<TNode>>>();

    /// <summary>The schema every array element follows (<c>items</c> as one schema).</summary>
    public SchemaNode<TNode>? Items { get; init; }

    /// <summary>Positional schemas: <c>prefixItems</c>, or draft-07 <c>items</c> written as an array.</summary>
    public IReadOnlyList<SchemaNode<TNode>> PrefixItems { get; init; } = Array.Empty<SchemaNode<TNode>>();

    /// <summary><c>additionalProperties</c>: null when absent, otherwise allowed or forbidden.</summary>
    public bool? AdditionalAllowed { get; init; }

    /// <summary><c>additionalProperties</c> given as a schema rather than a boolean.</summary>
    public SchemaNode<TNode>? AdditionalSchema { get; init; }

    /// <summary>A local <c>$ref</c> (<c>#</c> or <c>#/…</c>) — resolved on demand, never eagerly.</summary>
    public string? Ref { get; init; }

    public IReadOnlyList<SchemaNode<TNode>> AllOf { get; init; } = Array.Empty<SchemaNode<TNode>>();
    public IReadOnlyList<SchemaNode<TNode>> OneOf { get; init; } = Array.Empty<SchemaNode<TNode>>();
    public IReadOnlyList<SchemaNode<TNode>> AnyOf { get; init; } = Array.Empty<SchemaNode<TNode>>();

    /// <summary><c>then</c> / <c>else</c> of a conditional. <c>if</c> is never read: no validation.</summary>
    public IReadOnlyList<SchemaNode<TNode>> Conditional { get; init; } = Array.Empty<SchemaNode<TNode>>();

    /// <summary>The first non-<c>"null"</c> entry of <c>type</c>, if any.</summary>
    public string? Type { get; init; }

    public bool HasDefault { get; init; }
    public TNode? Default { get; init; }

    /// <summary>
    /// <c>const</c> and single/multi-valued <c>enum</c> constraints on this schema, as a list of
    /// allowed values. Unused by phase 1; it is what a discriminator selector reads in phase 2
    /// to tell the branches of a tagged union apart (<c>{"kind": {"const": "car"}}</c>).
    /// </summary>
    public IReadOnlyList<TNode> AllowedValues { get; init; } = Array.Empty<TNode>();
}
