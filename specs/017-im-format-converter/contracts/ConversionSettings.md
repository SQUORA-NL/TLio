# Contract: ConversionSettings

**Project**: `FormatConverter.Core`  
**Namespace**: `FormatConverter.Core`

## Purpose

Carries per-conversion-boundary adapter options from a `ConvertCommand` through `MultiFormatScriptRunner` into both `IFormatAdapter.FromIM` (source) and `IFormatAdapter.ToIM` (target). Settings scope to a single boundary — they do not persist to later `convert` commands.

## API

```csharp
public sealed class ConversionSettings
{
    public static ConversionSettings Empty { get; } = new();

    // XML settings
    public string TextProperty      { get; init; } = "#text";
    public string AttributePrefix   { get; init; } = "@";
    public string NamespacePrefix   { get; init; } = "xmlns:";
    public bool   InferTypes        { get; init; } = false;
    public bool   CdataAsText       { get; init; } = false;

    // YAML settings
    public bool   FlattenAnchors    { get; init; } = true;

    // JSON settings — reserved for future use; none mandatory in v1
}
```

## Contracts

- `ConversionSettings` is immutable (`init`-only properties).
- Passing `ConversionSettings.Empty` to an adapter is equivalent to omitting the `settings` block on a `convert` command — all defaults apply.
- Adapters MUST NOT throw for settings they do not use; they silently ignore irrelevant fields.
- Unknown JSON keys in the `settings` block of a `convert` command are logged as warnings and do not produce an error.

## Parsing from ConvertCommand JSON

```csharp
var settings = new ConversionSettings
{
    TextProperty    = json.GetStringOrDefault("textProperty", "#text"),
    AttributePrefix = json.GetStringOrDefault("attributePrefix", "@"),
    InferTypes      = json.GetBoolOrDefault("inferTypes", false),
    FlattenAnchors  = json.GetBoolOrDefault("flattenAnchors", true),
    // ...
};
```
