# Contract: IFormatAdapter

**Project**: `FormatConverter.Core`  
**Namespace**: `FormatConverter.Core`

## Purpose

The single extension point for adding new format support. Implement this interface and register it with `FormatConverter.Register(adapter)` to make the format available as both a conversion source and target.

## Interface

```csharp
public interface IFormatAdapter
{
    /// <summary>
    /// Unique, case-insensitive format identifier used for lookup.
    /// Examples: "json", "xml", "yaml", "csv", "edi-x12"
    /// </summary>
    string FormatId { get; }

    /// <summary>
    /// Parse a serialized document into an IntermediateNode tree.
    /// </summary>
    /// <param name="source">The complete serialized document string.</param>
    /// <returns>The root IntermediateNode of the parsed tree.</returns>
    /// <exception cref="FormatParseException">Input is malformed or cannot be parsed.</exception>
    IntermediateNode ToIM(string source);

    /// <summary>
    /// Serialize an IntermediateNode tree to this format.
    /// </summary>
    /// <param name="root">The root node of the IM tree.</param>
    /// <returns>The serialized document string.</returns>
    /// <exception cref="FormatParseException">IM contains constructs unrepresentable in this format without a defined fallback.</exception>
    string FromIM(IntermediateNode root);
}
```

## Contracts

- `FormatId` MUST be non-null, non-empty, and stable for the lifetime of the adapter instance.
- `ToIM` MUST be deterministic: the same input always produces a structurally equivalent IM tree.
- `FromIM` MUST produce a valid, parseable document for the target format.
- Adapters MUST be stateless between calls (no mutable instance state that varies per call).
- Adapters MUST NOT swallow parse errors — always throw `FormatParseException` with a descriptive message.

## Implementing a Custom Adapter

```csharp
public sealed class CsvFormatAdapter : IFormatAdapter
{
    public string FormatId => "csv";

    public IntermediateNode ToIM(string source)
    {
        // Parse CSV → ArrayNode of ObjectNodes (one per row)
        var root = new ArrayNode { Name = "rows" };
        // ... parsing logic using only .NET built-ins ...
        return root;
    }

    public string FromIM(IntermediateNode root)
    {
        // Serialize IM → CSV
        // ...
        return csvString;
    }
}

// Registration:
var converter = new FormatConverter();
converter.Register(new CsvFormatAdapter());
```
