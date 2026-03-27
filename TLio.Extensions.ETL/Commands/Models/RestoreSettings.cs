namespace TLio.Extensions.ETL.Commands.Models;

/// <summary>Controls how Restore reconstructs a previously flattened node.</summary>
public class RestoreSettings
{
    public string MetadataPath { get; set; } = "";
    public string MetadataKey { get; set; } = "_flattenMetadata";
    public bool RemoveMetadata { get; set; } = true;
    public bool UseJsonPathColumn { get; set; } = false;
    public string JsonPathColumn { get; set; } = "_jsonpath";
    public string Delimiter { get; set; } = ".";
    public string ArrayDelimiter { get; set; } = ".";
    public bool StrictMode { get; set; } = false;
}
