using System.Text.Json.Nodes;
using TLio.JsonPath.Internal;

namespace TLio.JsonPath;

/// <summary>
/// One node selected by a query, with where it was found. System.Text.Json has no node for JSON
/// <c>null</c> — it is stored as a C# <c>null</c> — so a match whose <see cref="Node"/> is null is a
/// hit on a JSON null, and <see cref="Parent"/> together with <see cref="Name"/> or <see cref="Index"/>
/// identifies the slot it occupies. A query that matches nothing returns no matches at all; that is
/// how "present with a null value" is told apart from "absent".
/// </summary>
public readonly struct JsonPathMatch
{
    private readonly Loc? _loc;

    internal JsonPathMatch(JsonNode? node, Loc? loc, bool isPropertyToken = false)
    {
        Node = node;
        _loc = loc;
        IsPropertyToken = isPropertyToken;
    }

    /// <summary>The selected node. <c>null</c> means the query selected a JSON null value.</summary>
    public JsonNode? Node { get; }

    /// <summary>The object or array that contains the node; null for the root.</summary>
    public JsonNode? Parent => _loc?.Container as JsonNode;

    /// <summary>The property name when the node is an object member, otherwise null.</summary>
    public string? Name => _loc?.Name;

    /// <summary>The position when the node is an array element, otherwise -1.</summary>
    public int Index => _loc is { Name: null, Up: not null } ? _loc.Index : -1;

    /// <summary>True for the document root (<c>$</c>).</summary>
    public bool IsRoot => _loc is { Up: null };

    /// <summary>The RFC 9535 §2.7 normalized path of the node, e.g. <c>$['store']['book'][0]</c>.</summary>
    public string NormalizedPath => _loc?.ToNormalizedPath() ?? "$";

    /// <summary>
    /// Newtonsoft dialect only. Newtonsoft's filter on an <em>object</em> walks its <c>JProperty</c> wrappers, so
    /// an expression such as <c>$.obj[?(@ != 1)]</c> selects the property itself, which has no System.Text.Json
    /// equivalent. The match then carries the property's value in <see cref="Node"/> and sets this flag.
    /// </summary>
    public bool IsPropertyToken { get; }
}
