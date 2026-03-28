# Data Model: Sample Projects — API and CLI

**Branch**: `005-samples-api-cli` | **Date**: 2026-03-28

The samples are stateless transformation pipelines — there is no persistence layer. The "entities" are value objects that flow through a single request/invocation.

---

## Entities

### SupportedFormat (enum)

Represents the detected or declared data format.

| Value | File Extensions | Content-Type | Execution Context |
|---|---|---|---|
| `Json` | `.json` | `application/json` | `JsonExecutionContext.CreateDefault()` |
| `Xml` | `.xml` | `application/xml` | `XmlExecutionContext.CreateWithNativeXPath()` |
| `Yaml` | `.yaml`, `.yml` | `text/yaml` | `YamlExecutionContext.CreateDefault()` |

**Validation**: If the extension does not match any known value → error, exit code 3 (CLI) or 415 (API).

---

### TransformationRequest (value object — API)

Represents one inbound HTTP transformation request.

| Field | Type | Source | Constraints |
|---|---|---|---|
| `Format` | `SupportedFormat` | Route parameter (`/transform/{format}`) | Must be a known format |
| `Payload` | string | HTTP request body (raw) | Must not be empty |
| `Script` | string (JSON) | Loaded from `Scripts/transform-{format}.json` at startup | Must be a valid TLio script array |

---

### TransformationInput (value object — CLI)

Represents one CLI invocation's parsed arguments.

| Field | Type | Source | Constraints |
|---|---|---|---|
| `InputPath` | string | `--input` argument | File must exist |
| `ScriptPath` | string | `--script` argument | File must exist |
| `OutputPath` | string? | `--output` argument (optional) | Parent directory must be writable if provided |
| `Format` | `SupportedFormat` | Derived from `InputPath` extension | Must resolve to a known format |

---

### TransformationResult (value object — shared)

Produced by `ScriptEngine<TNode>.Execute()`.

| Field | Type | Notes |
|---|---|---|
| `Success` | bool | `true` if execution completed without engine-level failure |
| `Data` | TNode | The output document after transformation |
| `LogEntries` | `IReadOnlyList<LogEntry>` | All Info/Warning/Error entries recorded during execution |

**Mapping to HTTP response**:
- `Success = true` → serialize `Data` to string, return 200 with appropriate `Content-Type`
- `Success = false` → return 422 with log entries in error body
- Parse failure before `Execute()` is called → return 400

**Mapping to CLI exit codes**: see `contracts/cli.md`.

---

## Lifecycle

```
[Raw input string]
    ↓ parse via adapter
[TNode (input document)]
    ↓ ScriptEngine.Execute(script, input, context)
[TransformationResult]
    ↓ serialize via adapter
[Raw output string]
```

No state persists between invocations. Each request/CLI run is independent.
