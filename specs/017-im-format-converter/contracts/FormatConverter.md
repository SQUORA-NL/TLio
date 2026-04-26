# Contract: FormatConverter

**Project**: `FormatConverter.Core`  
**Namespace**: `FormatConverter.Core`

## Purpose

The primary entry point for all format conversions. Holds a registry of `IFormatAdapter` instances and dispatches `ToIM` / `FromIM` calls to the correct adapter by format ID.

## Public API

```csharp
public sealed class FormatConverter
{
    /// <summary>
    /// Register (or replace) an adapter for its FormatId.
    /// Registering a second adapter with the same FormatId silently replaces the first.
    /// Follows TLio command/function registration conventions.
    /// </summary>
    public void Register(IFormatAdapter adapter);

    /// <summary>
    /// Parse a source document into an IntermediateNode tree using the registered adapter.
    /// </summary>
    /// <param name="formatId">Case-insensitive format identifier (e.g., "json").</param>
    /// <param name="source">Serialized document string.</param>
    /// <returns>Root IntermediateNode of the parsed tree.</returns>
    /// <exception cref="FormatNotRegisteredException">No adapter registered for formatId.</exception>
    /// <exception cref="FormatParseException">Source document is malformed.</exception>
    public IntermediateNode ToIM(string formatId, string source);

    /// <summary>
    /// Serialize an IntermediateNode tree to a target format.
    /// </summary>
    /// <param name="formatId">Case-insensitive format identifier (e.g., "xml").</param>
    /// <param name="root">Root node of the IM tree.</param>
    /// <returns>Serialized document string.</returns>
    /// <exception cref="FormatNotRegisteredException">No adapter registered for formatId.</exception>
    /// <exception cref="FormatParseException">IM cannot be serialized to the target format.</exception>
    public string FromIM(string formatId, IntermediateNode root);

    /// <summary>
    /// Convenience method: parse source as sourceFormatId, then serialize to targetFormatId.
    /// Equivalent to: FromIM(targetFormatId, ToIM(sourceFormatId, source))
    /// </summary>
    public string Convert(string sourceFormatId, string source, string targetFormatId);
}
```

## Format ID Conventions

| Format | FormatId | Adapter project |
|--------|----------|----------------|
| JSON (System.Text.Json) | `"json"` | `FormatConverter.Json` |
| XML (System.Xml) | `"xml"` | `FormatConverter.Xml` |
| YAML (YamlDotNet) | `"yaml"` | `FormatConverter.Yaml` |

Format IDs are case-insensitive (`"JSON"` == `"json"`).

## Usage Pattern

```csharp
// 1. Create and configure the converter (one-time setup)
var converter = new FormatConverter();
converter.Register(new JsonFormatAdapter());
converter.Register(new XmlFormatAdapter());
converter.Register(new YamlFormatAdapter());

// 2a. Two-step conversion (when you need the IM node for inspection/manipulation)
IntermediateNode im = converter.ToIM("json", jsonString);
string xmlOutput   = converter.FromIM("xml", im);

// 2b. Single-step convenience conversion
string xmlOutput   = converter.Convert("json", jsonString, "xml");

// 3. Swap implementation (e.g., custom licensed XML adapter)
converter.Register(new MyLicensedXmlAdapter()); // replaces the built-in XmlFormatAdapter
```

## Contracts

- `Register` is not thread-safe during configuration; once configured, `ToIM` and `FromIM` are safe to call concurrently from multiple threads (adapters must be stateless).
- `FormatId` matching is case-insensitive and trimmed.
- `Convert` is a pure convenience wrapper — it imposes no additional contracts beyond those of `ToIM` and `FromIM`.
