# Research: Universal Format Converter via Intermediate Model

**Branch**: `017-im-format-converter` | **Date**: 2026-04-25 (revised)

## 1. Intermediate Model Design — Industry Survey

### Decision
Adopt a **polymorphic node tree** with five concrete node kinds: `ObjectNode`, `ArrayNode`, `ScalarNode`, `MixedContentNode`, and a shared `NodeMetadata` bag. The IM is an internal transport mechanism — script authors never interact with IM nodes directly.

### Format Survey

| Format | Container | Sequence | Scalar | Format-Specific |
|--------|-----------|----------|--------|-----------------|
| JSON | Object `{}` | Array `[]` | string / number / bool / null | — |
| XML | Element | (repeated elements) | Text node | Attributes, namespaces, CDATA, PIs, mixed content |
| YAML | Mapping | Sequence | Scalar (untyped unless tagged) | Anchors/aliases, tags |
| EDI X12 | Transaction set / Segment | Element group | Data element (string) | Envelope hierarchy (ISA/GS/ST), delimiters |

### Rationale
- `ObjectNode` with an ordered `IList<IntermediateNode>` covers all container types.
- `ArrayNode` covers all sequence types.
- `ScalarNode` with `ScalarType` + `RawValue: string` covers all leaves without boxing.
- `MixedContentNode` handles XML-specific text/element interleaving.
- `NodeMetadata` carries everything format-specific without polluting core types.

---

## 2. Segmented Pipeline Execution Model

### Decision
The script runner (**`MultiFormatScriptRunner`**) pre-processes a TLio script to find `convert` commands, splits the command list into **sections**, and executes each section with its own strongly-typed `IExecutionContext<TNode>`. At each boundary, the current working document is converted through the IM using `FormatConverter`. The IM never surfaces to the script author.

### Script Example

```json
[
  { "command": "set", "path": "/person/name", "value": "Alice" },
  {
    "command": "convert",
    "to": "json",
    "settings": { "textProperty": "#text", "attributePrefix": "@" }
  },
  { "command": "set", "path": "$.person.name", "value": "Bob" },
  { "command": "convert", "to": "yaml" },
  { "command": "set", "path": "person.name", "value": "Carol" }
]
```

Section 1 (XML context): commands before first `convert`  
Boundary 1: XML→JSON via IM, settings applied  
Section 2 (JSON context): commands until second `convert`  
Boundary 2: JSON→YAML via IM, default settings  
Section 3 (YAML context): remaining commands

### Rationale
- Script authors think in their format's native path syntax within each section.
- No special "IM function call" syntax needed — just a single `convert` command.
- Entire working document is replaced at each boundary (no partial conversion).
- The runner handles context factory creation; script authors register adapters once.

### Alternatives Considered
- **In-script `ToIM`/`FromIM` function calls**: Rejected — requires script authors to understand IM nodes; more verbose; breaks the "just add a convert command" simplicity.
- **Host-level pipeline (pre/post wrapping)**: Rejected — doesn't support mid-script format switching; requires host code changes per conversion.

---

## 3. ConversionSettings — Per-Command Adapter Configuration

### Decision
A `ConversionSettings` object is carried by every `ConvertCommand` and passed to both `IFormatAdapter.FromIM` (source) and `IFormatAdapter.ToIM` (target) at each boundary. Settings have documented defaults; omitting a setting applies the default.

### Default Values Table

| Setting | Format | Default | Description |
|---------|--------|---------|-------------|
| `textProperty` | XML | `"#text"` | Key for XML text content in non-XML formats |
| `attributePrefix` | XML | `"@"` | Prefix for XML attribute keys |
| `namespacePrefix` | XML | `"xmlns:"` | Prefix for namespace declaration keys |
| `inferTypes` | XML, YAML | `false` | Coerce untyped strings to typed scalars |
| `cdataAsText` | XML | `false` | Treat CDATA as plain text (no `#cdata` metadata) |
| `flattenAnchors` | YAML | `true` | Dereference YAML anchors inline |

### Rationale
- Per-command settings mean each conversion step in a multi-step pipeline can use different conventions without re-registering adapters.
- Documented defaults keep the `convert` command concise for the common case.
- Settings are scoped to a single boundary — they don't leak to subsequent `convert` commands.

### Alternatives Considered
- **Registration-time settings only**: Rejected — prevents per-step customisation; requires re-registering adapters for different conventions.
- **Both registration-time defaults + per-command overrides**: Rejected — adds stateful config at registration; unnecessary complexity given per-command approach covers all cases.

---

## 4. XML Attribute Convention (Badgerfish)

### Decision
Default `attributePrefix = "@"` (Badgerfish). Overridable per `convert` command via `settings.attributePrefix`.

### Mapping Table (defaults)

| XML construct | IM representation | JSON/YAML representation |
|---|---|---|
| `<item id="42">` | `ObjectNode("item")` + `Metadata["@id"] = "42"` | `{ "item": { "@id": "42" } }` |
| `xmlns:ns="http://..."` | `Metadata["xmlns:ns"] = "http://..."` | `{ "@xmlns:ns": "http://..." }` |
| `<item>text</item>` | `ScalarNode("item", String, "text")` | `{ "item": "text" }` |
| `<item>text<b/>more</item>` | `MixedContentNode("item")` | `{ "item": { "#text": ["text", {}, "more"] } }` |
| `<![CDATA[raw]]>` | `ScalarNode` + `Metadata["#cdata"] = "true"` | string value |

---

## 5. Scalar Type Inference

### Decision
`inferTypes` defaults to `false`. When `true` (set per `convert` command), adapters apply:

| Raw value | Inferred ScalarType |
|-----------|---------------------|
| `"true"` / `"false"` (case-insensitive) | `Boolean` |
| Integer-parseable (no `.`) | `Integer` |
| Decimal-parseable | `Decimal` |
| `"null"` / `"~"` (YAML) | `Null` |
| Anything else | `String` (unchanged) |

---

## 6. YamlDotNet License

### Decision
YamlDotNet is **MIT licensed** — no restrictions. Explicit adapter registration still applies for swappability.

---

## 7. Registration Pattern — Alignment with TLio Conventions

### Decision
`FormatConverter.TLio` registers `ConvertCommand` using TLio's existing command registration mechanism. Format adapters are registered on a `FormatConverter` instance that is injected into the `MultiFormatScriptRunner`. The `MultiFormatScriptRunner` is registered as the script execution engine for multi-format scripts.

---

## 8. IFormatAdapter Signature with ConversionSettings

### Decision
Both `ToIM` and `FromIM` accept a `ConversionSettings` parameter (never null; empty = all defaults):

```csharp
public interface IFormatAdapter
{
    string FormatId { get; }
    IntermediateNode ToIM(string source, ConversionSettings settings);
    string FromIM(IntermediateNode root, ConversionSettings settings);
}
```

### Rationale
- Settings flow through the full call chain: `ConvertCommand` → `MultiFormatScriptRunner` → `FormatConverter` → `IFormatAdapter`.
- Adapters can safely ignore settings they don't understand (log a warning for unknown keys).
- Empty/null settings always produce the documented default behaviour.

---

## 9. ConvertCommand — ai-ref.md (Article XI)

### Decision
`ConvertCommand.md` in `docs/ai-ref/commands/` is required before the `FormatConverter.TLio` package merges into TLio. Content (≤150 lines):

```markdown
# ConvertCommand

> Switches the working document to a new format at the current script position.

## Syntax

{ "command": "convert", "to": "<formatId>", "settings": { ... } }

## Options

| Option | Type | Required | Default | Description |
|--------|------|----------|---------|-------------|
| to | string | yes | — | Target format ID ("json", "xml", "yaml") |
| settings.textProperty | string | no | "#text" | Key for XML text content |
| settings.attributePrefix | string | no | "@" | Prefix for XML attribute keys |
| settings.namespacePrefix | string | no | "xmlns:" | Prefix for XML namespace keys |
| settings.inferTypes | boolean | no | false | Coerce untyped strings to typed scalars |
| settings.cdataAsText | boolean | no | false | Treat CDATA as plain text |
| settings.flattenAnchors | boolean | no | true | Dereference YAML anchors inline |

## Example

{ "command": "convert", "to": "xml", "settings": { "attributePrefix": "@", "inferTypes": false } }
```
