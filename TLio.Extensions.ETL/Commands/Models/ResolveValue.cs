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
}
