namespace TLio.Core.Contracts;

/// <summary>
/// Provides all format-specific node manipulation operations so that commands
/// and functions can remain completely agnostic about the underlying data format.
///
/// Commands query and mutate nodes exclusively through this interface — there is
/// no direct dependency on JToken, XElement, YamlNode, or any other concrete type.
/// </summary>
/// <typeparam name="TNode">Native node type of the target data format.</typeparam>
public interface INodeAdapter<TNode>
{
    // ── Type queries ─────────────────────────────────────────────────────────

    bool IsObject(TNode node);
    bool IsArray(TNode node);
    bool IsPrimitive(TNode node);
    bool IsNull(TNode node);

    /// <summary>
    /// The kind of value the node holds. The default implementation derives it from the
    /// other type queries, which is the correct answer for untyped formats (XML, YAML).
    /// Adapters over a typed format (JSON) should override this to report the document's
    /// own type, so that the string "42" is reported as <see cref="NodeKind.String"/>.
    /// </summary>
    NodeKind GetNodeKind(TNode node)
    {
        if (IsNull(node)) return NodeKind.Null;
        if (IsObject(node)) return NodeKind.Object;
        if (IsArray(node)) return NodeKind.Array;
        if (TryGetBoolean(node).HasValue) return NodeKind.Boolean;
        if (TryGetDouble(node).HasValue) return NodeKind.Number;
        return NodeKind.String;
    }

    // ── Object operations ─────────────────────────────────────────────────────

    bool HasProperty(TNode node, string propertyName);
    TNode? GetProperty(TNode node, string propertyName);
    void SetProperty(TNode node, string propertyName, TNode value);
    void RemoveProperty(TNode node, string propertyName);
    IEnumerable<string> GetPropertyNames(TNode node);

    // ── Array operations ──────────────────────────────────────────────────────

    void AppendToArray(TNode array, TNode value);
    void InsertIntoArray(TNode array, int index, TNode value);
    void RemoveFromArray(TNode array, int index);
    int GetArrayLength(TNode array);
    TNode GetArrayElement(TNode array, int index);
    IEnumerable<TNode> GetArrayElements(TNode array);

    // ── Node creation ─────────────────────────────────────────────────────────

    TNode CreateNull();
    TNode CreateObject();
    TNode CreateArray();
    TNode CreateString(string value);
    TNode CreateNumber(double value);
    TNode CreateBoolean(bool value);
    TNode CreateValue(object? value);

    // ── Value access ──────────────────────────────────────────────────────────

    object? GetValue(TNode node);
    T? GetValue<T>(TNode node);

    // ── Type coercion ─────────────────────────────────────────────────────────
    // Required by PropertyChangeCommand and DecisionTable for type-safe comparisons.

    /// <summary>
    /// Try to convert the node to a boolean. Returns null if not representable.
    /// </summary>
    bool? TryGetBoolean(TNode node);

    /// <summary>
    /// Try to convert the node to a double. Returns null if not representable.
    /// </summary>
    double? TryGetDouble(TNode node);

    /// <summary>
    /// Try to convert the node to a string. Returns null only if truly null/missing.
    /// </summary>
    string? TryGetString(TNode node);

    // ── Cloning & replacement ─────────────────────────────────────────────────

    TNode DeepClone(TNode node);

    /// <summary>
    /// Replace <paramref name="target"/> in-place with <paramref name="replacement"/>.
    /// The semantics depend on the format — for JSON this swaps the JToken in its
    /// parent container; for XML it replaces the element in the document.
    /// </summary>
    void Replace(TNode target, TNode replacement);

    /// <summary>
    /// Remove <paramref name="node"/> from its parent (property or array element).
    /// Returns false if the node cannot be removed (e.g. it is the root).
    /// </summary>
    bool RemoveFromParent(TNode node);

    /// <summary>
    /// Change the name under which <paramref name="node"/> is known, keeping its value,
    /// children and position. Where the name lives is format-specific: an XML element
    /// carries its own name (so even the document element can be renamed), while a JSON
    /// or YAML node is named by the parent that holds it (so a root object has no name).
    ///
    /// Returns false when the node has no name to change or the new name is unusable —
    /// the Rename command turns that into a warning rather than a failure.
    /// </summary>
    bool RenameNode(TNode node, string newName) => false;

    // ── Deep merge ────────────────────────────────────────────────────────────
    // Required by CopyMove (root merge) and the Merge command.

    /// <summary>
    /// Recursively merge <paramref name="source"/> into <paramref name="target"/>.
    /// Object properties are merged recursively; array behaviour is controlled by
    /// <paramref name="arrayMergeMode"/>.
    /// </summary>
    void DeepMergeInto(TNode source, TNode target, ArrayMergeMode arrayMergeMode = ArrayMergeMode.Concat);

    // ── Parent access ─────────────────────────────────────────────────────────
    // Required by ScriptPath function and parent-navigation in commands.

    /// <summary>
    /// Return the logical parent node (skipping intermediate property wrappers
    /// in formats like Newtonsoft's JProperty).  Returns null for root.
    /// </summary>
    TNode? GetParentNode(TNode node);

    /// <summary>
    /// Return the property name under which <paramref name="node"/> lives in its
    /// parent object, or null if it lives in an array or has no parent.
    /// </summary>
    string? GetParentPropertyName(TNode node);

    // ── Equality ─────────────────────────────────────────────────────────────

    /// <summary>Deep-equality comparison (used by IfElse command).</summary>
    bool DeepEquals(TNode a, TNode b);

    // ── Serialisation ─────────────────────────────────────────────────────────

    TNode Parse(string content);
    string Serialize(TNode node, bool pretty = false);
}

/// <summary>Controls how array children are treated during a deep merge.</summary>
public enum ArrayMergeMode
{
    /// <summary>Append source array elements to the target array.</summary>
    Concat,
    /// <summary>Replace the target array entirely with the source array.</summary>
    Replace,
    /// <summary>Merge by matching elements using key paths (used by the Merge command).</summary>
    MergeByKey
}
