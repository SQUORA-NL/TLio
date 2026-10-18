using System.Text.Json;
using TLio.Core.Contracts;

namespace TLio.Functions.Schema;

/// <summary>
/// A schema read out of a document node: parses it into <see cref="SchemaNode{TNode}"/>s and
/// follows local <c>$ref</c>s.
///
/// The schema is read through <see cref="INodeAdapter{TNode}"/>, so it can live in a JSON, XML
/// or YAML document and be addressed by the same path language as the data it describes. Only
/// local references (<c>#</c> and <c>#/json/pointer</c>) are followed; anything else is reported
/// through <c>warn</c> and treated as "describes nothing" — a remote reference would make a
/// transformation depend on the network.
///
/// One instance belongs to one function call: the reference cache is not shared, so concurrent
/// executions of a compiled script never touch the same instance.
/// </summary>
public sealed class SchemaDocument<TNode>
{
    private readonly TNode _root;
    private readonly INodeAdapter<TNode> _adapter;
    private readonly Action<string> _warn;
    private readonly Dictionary<string, SchemaNode<TNode>?> _refs = new(StringComparer.Ordinal);
    private SchemaNode<TNode>? _rootSchema;

    public SchemaDocument(TNode root, INodeAdapter<TNode> adapter, Action<string> warn)
    {
        _root = root;
        _adapter = adapter;
        _warn = warn;
    }

    public SchemaNode<TNode> Root => _rootSchema ??= Parse(_root);

    // ── $ref ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Resolve a local reference. Results are cached by reference string, so two places that
    /// point at the same definition get the <b>same instance</b> — which is what lets a caller
    /// recognise that it has come round a recursive schema.
    /// </summary>
    public bool TryResolveRef(string reference, out SchemaNode<TNode> node)
    {
        if (_refs.TryGetValue(reference, out var cached))
        {
            node = cached!;
            return cached is not null;
        }

        var resolved = ResolveUncached(reference);
        _refs[reference] = resolved;
        node = resolved!;
        return resolved is not null;
    }

    private SchemaNode<TNode>? ResolveUncached(string reference)
    {
        if (reference == "#") return Root;

        if (!reference.StartsWith("#/", StringComparison.Ordinal))
        {
            _warn($"$ref '{reference}' is not a local reference (only '#' and '#/…' are followed) — ignored.");
            return null;
        }

        var current = _root;
        foreach (var raw in reference[2..].Split('/'))
        {
            var segment = Uri.UnescapeDataString(raw).Replace("~1", "/").Replace("~0", "~");

            if (_adapter.IsObject(current) && _adapter.GetProperty(current, segment) is { } child)
            {
                current = child;
            }
            else if (_adapter.IsArray(current) && int.TryParse(segment, out var index)
                     && index >= 0 && index < _adapter.GetArrayLength(current))
            {
                current = _adapter.GetArrayElement(current, index);
            }
            else
            {
                _warn($"$ref '{reference}' does not resolve inside the schema — ignored.");
                return null;
            }
        }

        return Parse(current);
    }

    // ── Parsing ───────────────────────────────────────────────────────────────

    private SchemaNode<TNode> Parse(TNode schema)
    {
        // `true`, `false` and anything that is not an object describe no structure at all.
        if (!_adapter.IsObject(schema)) return new SchemaNode<TNode>();

        var hasProperties = false;
        var properties = new List<KeyValuePair<string, SchemaNode<TNode>>>();
        if (Get(schema, "properties") is { } props && _adapter.IsObject(props))
        {
            hasProperties = true;
            foreach (var name in _adapter.GetPropertyNames(props).ToList())
                if (_adapter.GetProperty(props, name) is { } sub)
                    properties.Add(new(name, Parse(sub)));
        }

        SchemaNode<TNode>? items = null;
        var prefix = new List<SchemaNode<TNode>>();
        if (Get(schema, "prefixItems") is { } prefixItems && _adapter.IsArray(prefixItems))
            prefix.AddRange(_adapter.GetArrayElements(prefixItems).Select(Parse));
        if (Get(schema, "items") is { } itemsNode)
        {
            if (_adapter.IsArray(itemsNode))
                prefix.AddRange(_adapter.GetArrayElements(itemsNode).Select(Parse)); // draft-07 tuple form
            else if (_adapter.IsObject(itemsNode))
                items = Parse(itemsNode);
        }

        bool? additionalAllowed = null;
        SchemaNode<TNode>? additionalSchema = null;
        if (Get(schema, "additionalProperties") is { } additional)
        {
            if (_adapter.IsObject(additional))
            {
                additionalSchema = Parse(additional);
                additionalAllowed = true;
            }
            else if (_adapter.GetNodeKind(additional) == NodeKind.Boolean)
            {
                additionalAllowed = _adapter.TryGetBoolean(additional);
            }
        }

        var conditional = new List<SchemaNode<TNode>>();
        if (Get(schema, "then") is { } then) conditional.Add(Parse(then));
        if (Get(schema, "else") is { } otherwise) conditional.Add(Parse(otherwise));

        var allowed = new List<TNode>();
        if (Get(schema, "const") is { } constant) allowed.Add(constant);
        if (Get(schema, "enum") is { } enumeration && _adapter.IsArray(enumeration))
            allowed.AddRange(_adapter.GetArrayElements(enumeration));

        var defaultNode = Get(schema, "default");

        return new SchemaNode<TNode>
        {
            HasProperties = hasProperties,
            Properties = properties,
            Items = items,
            PrefixItems = prefix,
            AdditionalAllowed = additionalAllowed,
            AdditionalSchema = additionalSchema,
            Ref = Get(schema, "$ref") is { } r ? _adapter.TryGetString(r) : null,
            AllOf = ParseList(schema, "allOf"),
            OneOf = ParseList(schema, "oneOf"),
            AnyOf = ParseList(schema, "anyOf"),
            Conditional = conditional,
            Type = ReadType(schema),
            HasDefault = defaultNode is not null,
            Default = defaultNode,
            AllowedValues = allowed
        };
    }

    private IReadOnlyList<SchemaNode<TNode>> ParseList(TNode schema, string keyword) =>
        Get(schema, keyword) is { } list && _adapter.IsArray(list)
            ? _adapter.GetArrayElements(list).Select(Parse).ToList()
            : Array.Empty<SchemaNode<TNode>>();

    private string? ReadType(TNode schema)
    {
        if (Get(schema, "type") is not { } type) return null;
        if (!_adapter.IsArray(type)) return _adapter.TryGetString(type);

        return _adapter.GetArrayElements(type)
            .Select(t => _adapter.TryGetString(t))
            .FirstOrDefault(t => t is not null && t != "null");
    }

    private TNode? Get(TNode node, string name) => _adapter.GetProperty(node, name);

    // ── Schema given as text ──────────────────────────────────────────────────

    /// <summary>
    /// Build a schema node from JSON text, whatever the document's own format is.
    ///
    /// <see cref="INodeAdapter{TNode}.Parse"/> would read the text in the <i>data's</i> format —
    /// as XML in an XML document — so the schema is parsed as JSON with the in-box parser and
    /// rebuilt node by node through the adapter, the same rule the script notations follow.
    /// Property order survives: <see cref="JsonDocument"/> enumerates in document order.
    /// </summary>
    public static TNode FromJsonText(string json, INodeAdapter<TNode> adapter)
    {
        using var document = JsonDocument.Parse(json, new JsonDocumentOptions
        {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        });
        return Build(document.RootElement, adapter);
    }

    private static TNode Build(JsonElement element, INodeAdapter<TNode> adapter)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                var obj = adapter.CreateObject();
                foreach (var property in element.EnumerateObject())
                    adapter.SetProperty(obj, property.Name, Build(property.Value, adapter));
                return obj;
            case JsonValueKind.Array:
                var array = adapter.CreateArray();
                foreach (var item in element.EnumerateArray())
                    adapter.AppendToArray(array, Build(item, adapter));
                return array;
            case JsonValueKind.String:
                return adapter.CreateString(element.GetString() ?? string.Empty);
            case JsonValueKind.Number:
                return element.TryGetInt64(out var whole)
                    ? adapter.CreateValue(whole)
                    : adapter.CreateNumber(element.GetDouble());
            case JsonValueKind.True:
                return adapter.CreateBoolean(true);
            case JsonValueKind.False:
                return adapter.CreateBoolean(false);
            default:
                return adapter.CreateNull();
        }
    }
}
