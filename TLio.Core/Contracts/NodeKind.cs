namespace TLio.Core.Contracts;

/// <summary>
/// The kind of value a node holds, used by the type-check predicates
/// (<c>=isString(...)</c>, <c>=isNumber(...)</c>, …).
///
/// Formats that carry types in the document (JSON) report the document's own type.
/// Untyped formats (XML, YAML scalars) report the value's apparent type — "42" reads
/// as <see cref="Number"/> there, because that is the only meaningful answer a format
/// without a type system can give.
/// </summary>
public enum NodeKind
{
    Null,
    Object,
    Array,
    String,
    Number,
    Boolean
}
