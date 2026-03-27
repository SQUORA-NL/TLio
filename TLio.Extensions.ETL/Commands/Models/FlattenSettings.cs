namespace TLio.Extensions.ETL.Commands.Models;

/// <summary>Controls how Flatten walks and encodes the node tree.</summary>
public class FlattenSettings
{
    public string Delimiter { get; set; } = ".";
    public string ArrayDelimiter { get; set; } = ".";
    public bool IncludeArrayIndices { get; set; } = true;
    public bool IncludeJsonPath { get; set; } = false;
    public string JsonPathColumn { get; set; } = "_jsonpath";
    public string MetadataPath { get; set; } = "$";
    public string MetadataKey { get; set; } = "_flattenMetadata";
    public bool PreserveTypes { get; set; } = true;
    public string TypeIndicator { get; set; } = "_type";
    public int MaxDepth { get; set; } = -1;   // -1 = unlimited
    public List<string> ExcludePaths { get; set; } = new();
    public List<string> IncludePaths { get; set; } = new();
}
