namespace TLio.Extensions.ETL.Commands.Models;

/// <summary>
/// One resolve rule: match target tokens to a reference collection using ResolveKeys,
/// then write the resolved values via Values.
/// </summary>
public class ResolveSetting<TNode>
{
    public List<ResolveKey> ResolveKeys { get; set; } = new();
    public string ReferencesCollectionPath { get; set; } = string.Empty;
    public List<ResolveValue<TNode>> Values { get; set; } = new();
}
