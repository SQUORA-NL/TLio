namespace TLio.Extensions.ETL.Commands.Models;

public enum ResolveTypeBehavior
{
    /// <summary>Always returns an array (empty when no matches).</summary>
    AlwaysAsArray,
    /// <summary>1 match → object, multiple → array, none → skip.</summary>
    DependingOnResult,
    /// <summary>Always returns a single object; throws if multiple matches.</summary>
    AlwaysAsObject,
}

/// <summary>
/// Defines what value to write and where after a successful resolve match.
/// </summary>
public class ResolveValue<TNode>
{
    public string TargetPath { get; set; } = string.Empty;
    public IFunctionSupportedValue<TNode>? Value { get; set; }
    public ResolveTypeBehavior ResolveTypeBehavior { get; set; } = ResolveTypeBehavior.DependingOnResult;

    /// <summary>
    /// Set only when <see cref="TargetPath"/> was written as a function expression (starts with
    /// "="), e.g. <c>"=fetch(@.to)"</c>. When present, the target property name is computed by
    /// evaluating this expression against the matched reference entry — the same way
    /// <see cref="Value"/> is evaluated — instead of reading <see cref="TargetPath"/> literally
    /// as an "@.property" walk. Requires exactly one match: the result names one property to
    /// write on the node currently being resolved, and there is no defined meaning for "the same
    /// dynamic name" written from several different matches at once.
    /// </summary>
    public IFunctionSupportedValue<TNode>? TargetPathExpression { get; set; }
}
