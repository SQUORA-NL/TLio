namespace TLio.Extensions.ETL.Commands.Models;

/// <summary>Round-trip metadata stored inside the data document after Flatten.</summary>
public class FlattenMetadata
{
    public Dictionary<string, string> OriginalStructure { get; set; } = new();
    public string? Delimiter { get; set; }
    public string? ArrayDelimiter { get; set; }
    public bool IncludeArrayIndices { get; set; }
    public bool PreserveTypes { get; set; }
    public string? TypeIndicator { get; set; }
    public string? Timestamp { get; set; }
    public string Version { get; set; } = "1.0";
    public string? RootPath { get; set; }
    public string? MetadataKey { get; set; }
}
