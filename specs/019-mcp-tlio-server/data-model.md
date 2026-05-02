# Data Model: TLio MCP Server (019)

## Core Observability Types (TLio.Core — additive)

### TraceOutcome

```csharp
// TLio.Core/Models/TraceOutcome.cs
public enum TraceOutcome { Success, NoOp, Failure }
```

| Value | Meaning |
|-------|---------|
| `Success` | Command matched ≥ 1 node and applied its operation without error |
| `NoOp` | Command matched 0 nodes; transformation skipped (not an error) |
| `Failure` | Command encountered an error (invalid path, type mismatch, argument error) |

---

### TraceEntry

```csharp
// TLio.Core/Models/TraceEntry.cs
public record TraceEntry(
    string CommandName,   // e.g. "Set", "Copy", "Add"
    string Path,          // path expression supplied to the command
    TraceOutcome Outcome,
    int MatchedCount,     // number of nodes selected by path
    string Detail         // human-readable message explaining what happened
);
```

Invariants:
- `MatchedCount == 0` when `Outcome == NoOp`
- `MatchedCount ≥ 1` when `Outcome == Success`
- `Detail` is never null; always a non-empty sentence

---

### ITraceCollector

```csharp
// TLio.Core/Contracts/ITraceCollector.cs
public interface ITraceCollector
{
    void Record(TraceEntry entry);
}
```

Used by: commands in `TLio.Commands` and extension packs, guarded with `context.TraceCollector?.Record(...)`.

---

### IExecutionContext<TNode> (modified — additive)

New nullable property added to the existing interface:

```csharp
ITraceCollector? TraceCollector { get; set; }
```

Default: `null` in all existing concrete context implementations → zero behaviour change for existing code.

---

## MCP Tool Request / Response Types (TLio.Mcp)

### ExecuteRequest

```csharp
public record ExecuteRequest(
    string Document,       // raw document text (JSON, XML, or YAML)
    string Format,         // "json" | "xml" | "yaml"
    string Script,         // TLio script JSON text
    string? XmlPathStyle   // "slash" (default) | "xpath" — only relevant when Format=="xml"
);
```

---

### CommandTraceRecord

```csharp
public record CommandTraceRecord(
    string CommandName,
    string Path,
    string Outcome,       // "success" | "noop" | "failure"
    int MatchedCount,
    string Detail
);
```

Serialised from `TraceEntry`. String `Outcome` (not enum) for clean JSON output to the agent.

---

### ExecuteResult

```csharp
public record ExecuteResult(
    bool Success,
    string Output,                           // transformed document text (same format as input)
    string Format,
    IReadOnlyList<CommandTraceRecord> Trace, // empty list when observability disabled
    IReadOnlyList<string> Errors             // execution-level errors (not per-command failures)
);
```

---

### ChangeType

```csharp
public enum ChangeType { Add, Remove, Rename, Mutate, Reorder }
```

| Value | Meaning |
|-------|---------|
| `Add` | A field/element present in target but absent in input |
| `Remove` | A field/element present in input but absent in target |
| `Rename` | A field key/element name changed; value is identical |
| `Mutate` | A field/element exists in both; its value changed |
| `Reorder` | Array elements are the same set but in a different order |

---

### ChangeItem

```csharp
public record ChangeItem(
    string SourcePath,        // path in input document (empty string for Add)
    string TargetPath,        // path in target document (empty string for Remove)
    string ChangeType,        // string form of ChangeType enum
    string Description,       // plain-language sentence: "Field 'name' renamed to 'fullName'"
    string? IntentAnnotation, // populated when intent parameter supplied and disambiguation possible
    string? Resolution        // "resolved" | "unresolved" — only populated in refinement mode
);
```

---

### AnalyzeRequest

```csharp
public record AnalyzeRequest(
    string Input,               // raw input document
    string InputFormat,         // "json" | "xml" | "yaml"
    string Target,              // raw target document
    string TargetFormat,        // "json" | "xml" | "yaml"
    string? Intent,             // optional plain-language transformation intent
    IReadOnlyList<CommandTraceRecord>? PriorTrace  // optional refinement context
);
```

---

### AnalyzeResult

```csharp
public record AnalyzeResult(
    IReadOnlyList<ChangeItem> Changes,
    string Summary,              // "3 changes required: 1 rename, 1 mutate, 1 add"
    int UnresolvedCount          // 0 unless PriorTrace was supplied; count of still-unresolved changes
);
```

---

## Configuration Model (TLio.Mcp)

### McpConfiguration

```csharp
public record McpConfiguration(
    ObservabilityConfig Observability,
    RateLimitConfig RateLimit,
    string AiRefRoot   // path to docs/ai-ref/ directory; default = relative to assembly location
);

public record ObservabilityConfig(bool Enabled);  // default: true

public record RateLimitConfig(
    int RequestsPerMinute,  // default: 20
    int WindowCount         // default: 6 (sub-windows per minute)
);
```

**`appsettings.json` shape**:
```json
{
  "Observability": { "Enabled": true },
  "RateLimit": { "RequestsPerMinute": 20, "WindowCount": 6 },
  "AiRefRoot": ""
}
```

**Environment variable overrides** (standard .NET config flattening with `__` separator):
- `TLIO_MCP_TRACE=false` → `Observability:Enabled = false` (custom mapping in Program.cs)
- `TLIO_MCP_RATELIMIT=50` → `RateLimit:RequestsPerMinute = 50`
