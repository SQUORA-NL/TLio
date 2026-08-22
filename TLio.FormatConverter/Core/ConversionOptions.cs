namespace TLio.FormatConverter.Core;

/// <summary>How a sequence is spelled in a format that has no sequences of its own.</summary>
public enum ArrayHandling
{
    /// <summary>
    /// The array is one element and its items are children of it:
    /// <c>&lt;lines&gt;&lt;item&gt;…&lt;/item&gt;&lt;item&gt;…&lt;/item&gt;&lt;/lines&gt;</c>.
    /// This is TLio's canonical document shape — the only one where the array has a node of its
    /// own for a path to address — and the default.
    /// </summary>
    Wrapped,

    /// <summary>
    /// The array has no element of its own; its items repeat under the parent:
    /// <c>&lt;lines&gt;…&lt;/lines&gt;&lt;lines&gt;…&lt;/lines&gt;</c>. Common in legacy schemas.
    /// Reading such a document back gives the array, but the *parent* then looks like an array
    /// too, so a TLio script cannot address either reliably. Use it to talk to a system that
    /// insists on it, not as a working shape.
    /// </summary>
    Repeated,
}

/// <summary>How an absent value is spelled in XML.</summary>
public enum NullRepresentation
{
    /// <summary>An empty element, <c>&lt;k/&gt;</c>. Indistinguishable from "", {} and [].</summary>
    Empty,

    /// <summary>
    /// <c>&lt;k xsi:nil="true"/&gt;</c>, which says null and nothing else. Costs a namespace
    /// declaration. <c>xsi:nil</c> is always honoured when reading, whichever setting is in force.
    /// </summary>
    XsiNil,
}

/// <summary>What to do with a property name that is not a legal XML element name.</summary>
public enum NameSanitization
{
    /// <summary>
    /// Rewrite it into something legal — spaces become underscores, a leading digit gains one.
    /// Quiet and lossy: <c>first name</c> and <c>first_name</c> both become <c>first_name</c>.
    /// </summary>
    Sanitize,

    /// <summary>Refuse the conversion and name the offending key.</summary>
    Error,

    /// <summary>
    /// Encode it reversibly using the XML Schema <c>_xHHHH_</c> convention, so the original name
    /// comes back on the return trip. The element names are ugly; the data survives.
    /// </summary>
    Escape,
}
