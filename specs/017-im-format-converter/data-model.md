# Data Model: Universal Format Converter via Intermediate Model

**Branch**: `017-im-format-converter` | **Date**: 2026-04-25 (revised)

## Intermediate Model (IM) — Core Type Hierarchy

```
IntermediateNode  (abstract)
├── Name: string?          — element/property name; null for array items
├── Metadata: NodeMetadata — format-specific key-value annotations
│
├── ObjectNode             — named container with ordered children
│   └── Children: IList<IntermediateNode>
│
├── ArrayNode              — ordered sequence of (possibly unnamed) items
│   └── Items: IList<IntermediateNode>
│
├── ScalarNode             — typed leaf value
│   ├── Type: ScalarType
│   └── RawValue: string?  — null only when Type = Null
│
└── MixedContentNode       — XML mixed content (text interleaved with elements)
    └── Content: IList<MixedContentItem>
        └── MixedContentItem = TextRun(string) | ChildNode(IntermediateNode)
```

> **Note**: The IM is an **internal transport** between script sections. Script authors never interact with IM nodes. Only `IFormatAdapter` implementations and `MultiFormatScriptRunner` touch IM objects.

---

## Entity Definitions

### `IntermediateNode` (abstract)

| Property | Type | Nullable | Description |
|----------|------|----------|-------------|
| `Name` | `string` | yes | Property/element name. Null for array items and document root. |
| `Metadata` | `NodeMetadata` | no | Always initialized; empty by default. |

---

### `ObjectNode : IntermediateNode`

Represents: JSON object, XML element, YAML mapping, EDI segment.

| Property | Type | Description |
|----------|------|-------------|
| `Children` | `IList<IntermediateNode>` | Ordered; repeated names allowed. |

---

### `ArrayNode : IntermediateNode`

Represents: JSON array, YAML sequence, EDI element group, CSV file.

| Property | Type | Description |
|----------|------|-------------|
| `Items` | `IList<IntermediateNode>` | Ordered; items may be unnamed. |

---

### `ScalarNode : IntermediateNode`

Represents: JSON string/number/bool/null, XML text node, YAML scalar, EDI element.

| Property | Type | Nullable | Description |
|----------|------|----------|-------------|
| `Type` | `ScalarType` | no | Declared type hint. |
| `RawValue` | `string` | yes | Canonical string form. Null only when `Type = Null`. |

**`ScalarType` Enum**: `String` | `Integer` | `Decimal` | `Boolean` | `Null`

---

### `MixedContentNode : IntermediateNode`

Represents: XML mixed content (`<p>text <b>bold</b> more</p>`).

| Property | Type | Description |
|----------|------|-------------|
| `Content` | `IList<MixedContentItem>` | Ordered text runs and child nodes. |

**Non-XML serialization**: collapses to concatenated string; sets `Metadata["#mixed"] = "true"`.

---

### `NodeMetadata : Dictionary<string, string>`

| Key pattern | Owner | Meaning |
|-------------|-------|---------|
| `@{name}` | XML adapter | XML attribute |
| `xmlns:{prefix}` | XML adapter | Namespace declaration |
| `xmlns` | XML adapter | Default namespace |
| `#cdata` | XML adapter | `"true"` when content was CDATA |
| `#pi:{target}` | XML adapter | Processing instruction data |
| `#mixed` | All adapters | `"true"` when mixed content was collapsed |
| `#anchor-flattened` | YAML adapter | `"true"` when anchor/alias was dereferenced |
| `#edi-type` | EDI adapter (future) | EDI structural role |

---

## ConversionSettings

Carried by every `ConvertCommand` and passed to both `IFormatAdapter.FromIM` (source) and `IFormatAdapter.ToIM` (target) at each conversion boundary. Settings are scoped to a single boundary step; they do not persist to subsequent `convert` commands.

| Setting | Type | Default | Format | Description |
|---------|------|---------|--------|-------------|
| `textProperty` | `string` | `"#text"` | XML | Key for XML text content in non-XML formats |
| `attributePrefix` | `string` | `"@"` | XML | Prefix prepended to XML attribute names |
| `namespacePrefix` | `string` | `"xmlns:"` | XML | Prefix for namespace declaration keys |
| `inferTypes` | `bool` | `false` | XML, YAML | Coerce untyped strings to typed scalars |
| `cdataAsText` | `bool` | `false` | XML | Treat CDATA as plain text (suppress `#cdata` metadata) |
| `flattenAnchors` | `bool` | `true` | YAML | Dereference anchors/aliases inline |

**Invariants**:
- `ConversionSettings` is never null; an empty instance applies all defaults.
- Unknown setting keys are logged as warnings and ignored (no error thrown).
- Adapters read only the settings relevant to their format; they ignore others.

---

## Adapter Interface

### `IFormatAdapter`

| Member | Signature | Description |
|--------|-----------|-------------|
| `FormatId` | `string { get; }` | Unique, case-insensitive format identifier. |
| `ToIM` | `IntermediateNode ToIM(string source, ConversionSettings settings)` | Parse source string → IM tree. |
| `FromIM` | `string FromIM(IntermediateNode root, ConversionSettings settings)` | Serialize IM tree → format string. |

**Contracts**:
- `settings` is never null; adapters call `settings.GetTextProperty()` (etc.) to read values with defaults baked in.
- `ToIM` throws `FormatParseException` on malformed input.
- `FromIM` throws `FormatParseException` if IM contains constructs unrepresentable without a defined fallback.

---

## FormatConverter

| Member | Signature | Description |
|--------|-----------|-------------|
| `Register` | `void Register(IFormatAdapter)` | Register or replace adapter for its `FormatId`. |
| `ToIM` | `IntermediateNode ToIM(string formatId, string source, ConversionSettings settings)` | Dispatch to registered adapter. |
| `FromIM` | `string FromIM(string formatId, IntermediateNode root, ConversionSettings settings)` | Dispatch to registered adapter. |
| `Convert` | `string Convert(string src, string doc, string tgt, ConversionSettings settings)` | Convenience: `FromIM(tgt, ToIM(src, doc, settings), settings)`. |

---

## ConvertCommand

A TLio command that signals a format boundary in a script. Intercepted by `MultiFormatScriptRunner` during script pre-processing rather than executed inline by the standard command dispatcher.

**JSON Schema**:
```json
{
  "command": "convert",
  "to": "<formatId>",
  "settings": {
    "textProperty": "#text",
    "attributePrefix": "@",
    "namespacePrefix": "xmlns:",
    "inferTypes": false,
    "cdataAsText": false,
    "flattenAnchors": true
  }
}
```

**Fields**:

| Field | Type | Required | Default | Description |
|-------|------|----------|---------|-------------|
| `command` | `string` | yes | — | Must be `"convert"` |
| `to` | `string` | yes | — | Target format ID |
| `settings` | `object` | no | all defaults | Per-boundary adapter settings |

---

## ScriptSection (internal)

One segment of a multi-format script between conversion boundaries.

| Property | Type | Description |
|----------|------|-------------|
| `FormatId` | `string` | Format identifier governing this section's `TNode` type. |
| `Commands` | `IList<ICommand>` | Ordered TLio commands to execute in this section. |
| `IncomingSettings` | `ConversionSettings?` | Settings from the preceding `convert` command (null for the first section). |

---

## MultiFormatScriptRunner

Orchestrates segmented script execution.

**Algorithm**:
1. Scan the flat command list; extract `ConvertCommand` positions.
2. Split into `ScriptSection` list: each section's `FormatId` starts as the initial input format; each `convert` command updates the format for the next section.
3. For each section:
   a. Create `IExecutionContext<TNode>` for the section's format using the registered context factory.
   b. Execute all commands in the section via the standard TLio engine.
   c. If a next section exists: call `FormatConverter.Convert(currentFormat, serialisedDoc, nextFormat, settings)` to produce the next section's input document; log the boundary operation.
4. Return the final document in the last section's format.

**Error behaviour**: If a `convert` command references an unregistered format, throw `FormatNotRegisteredException` at the boundary (not at script-load time).

---

## Exceptions

| Type | When thrown |
|------|-------------|
| `FormatNotRegisteredException` | `ToIM`/`FromIM` called with unregistered `formatId`. Includes `FormatId` and `RegisteredIds`. |
| `FormatParseException` | Adapter fails to parse input or serialise IM. Includes `FormatId`, `Operation` ("ToIM"/"FromIM"), inner exception. |
