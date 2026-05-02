# MCP Tool Contracts: TLio MCP Server (019)

All tools are exposed via MCP stdio transport. Tool names use snake_case with `tlio_` prefix.

---

## tlio_list_commands

Lists all available TLio commands with their names and one-line intent descriptions.

**Input**: none

**Output**:
```json
{
  "commands": [
    { "name": "Set",   "intent": "Sets the value of an existing node at the given path." },
    { "name": "Add",   "intent": "Adds a new property to an object or appends to an array." },
    ...
  ]
}
```

**Implementation**: Glob `{AiRefRoot}/commands/*.md`; extract filename (→ name) and first non-heading, non-blank line (→ intent).

**Rate limited**: Yes.

---

## tlio_list_functions

Lists all available TLio functions (built-in + all registered extension packs).

**Input**: none

**Output**:
```json
{
  "functions": [
    { "name": "concat", "intent": "Concatenates two or more string values." },
    { "name": "format",  "intent": "Formats a value using a .NET format string." },
    ...
  ]
}
```

**Implementation**: Glob `{AiRefRoot}/functions/*.md`; same extraction pattern.

**Rate limited**: Yes.

---

## tlio_describe

Returns the full ai-ref.md documentation for a named command, function, or adapter.

**Input**:
```json
{
  "name":  "Set",
  "type":  "command"
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `name` | string | yes | Exact name of command, function, or adapter slug |
| `type` | string | no | `"command"` \| `"function"` \| `"adapter"`. Auto-detected from filename if omitted. |

**Output**:
```json
{
  "name":    "Set",
  "type":    "command",
  "content": "# Set\n\n> Sets the value of an existing node …\n\n## Syntax\n…"
}
```

**Not-found response**:
```json
{
  "error":       "not_found",
  "name":        "Sett",
  "suggestions": ["Set", "Put"]
}
```
Suggestions: all names with Levenshtein distance ≤ 2 from the requested name.

**Rate limited**: Yes.

---

## tlio_execute

Executes a TLio script against an input document and returns the transformed output with an execution trace.

**Input**:
```json
{
  "document":      "{ \"name\": \"Alice\" }",
  "format":        "json",
  "script":        "[{ \"command\": \"set\", \"path\": \"$.name\", \"value\": \"Bob\" }]",
  "xml_path_style": "slash"
}
```

| Field | Type | Required | Default | Description |
|-------|------|----------|---------|-------------|
| `document` | string | yes | — | Raw document text |
| `format` | string | yes | — | `"json"` \| `"xml"` \| `"yaml"` |
| `script` | string | yes | — | TLio script as JSON text |
| `xml_path_style` | string | no | `"slash"` | `"slash"` \| `"xpath"` — only applies when `format == "xml"` |

**Output (success)**:
```json
{
  "success": true,
  "output":  "{ \"name\": \"Bob\" }",
  "format":  "json",
  "trace": [
    {
      "command_name":   "Set",
      "path":           "$.name",
      "outcome":        "success",
      "matched_count":  1,
      "detail":         "Set value to 'Bob' on 1 node at path '$.name'."
    }
  ],
  "errors": []
}
```

**Output (no-op example in trace)**:
```json
{
  "command_name":  "Set",
  "path":          "$.missing_field",
  "outcome":       "noop",
  "matched_count": 0,
  "detail":        "Path '$.missing_field' matched 0 nodes; no changes applied."
}
```

**Output (failure)**:
```json
{
  "success": false,
  "output":  "{ \"name\": \"Alice\" }",
  "format":  "json",
  "trace": [
    {
      "command_name":  "Set",
      "path":          "$.name",
      "outcome":       "failure",
      "matched_count": 0,
      "detail":        "Type mismatch: cannot set value on array node at '$.name'."
    }
  ],
  "errors": ["Execution halted after command 'Set' failed."]
}
```

**Observability disabled**: `trace` is an empty array `[]`; no instrumentation overhead.

**Rate limited**: Yes.

---

## tlio_analyze

Performs a deterministic structural diff between an input document and a target document. Returns a gap report describing every node-level change required to convert the input into the target.

**Input**:
```json
{
  "input":         "{ \"name\": \"Alice\", \"age\": 30 }",
  "input_format":  "json",
  "target":        "{ \"fullName\": \"Alice\", \"age\": 31 }",
  "target_format": "json",
  "intent":        "Rename 'name' to 'fullName' and increment age.",
  "prior_trace":   null
}
```

| Field | Type | Required | Default | Description |
|-------|------|----------|---------|-------------|
| `input` | string | yes | — | Raw input document |
| `input_format` | string | yes | — | `"json"` \| `"xml"` \| `"yaml"` |
| `target` | string | yes | — | Raw target document |
| `target_format` | string | yes | — | `"json"` \| `"xml"` \| `"yaml"` |
| `intent` | string | no | `null` | Plain-language description of intent; used to annotate ambiguous changes |
| `prior_trace` | array | no | `null` | Trace from a previous `tlio_execute` call; triggers refinement mode |

**Output**:
```json
{
  "changes": [
    {
      "source_path":       "$.name",
      "target_path":       "$.fullName",
      "change_type":       "Rename",
      "description":       "Field 'name' renamed to 'fullName' (value unchanged: 'Alice').",
      "intent_annotation": "Matches intent: 'Rename name to fullName'.",
      "resolution":        null
    },
    {
      "source_path":       "$.age",
      "target_path":       "$.age",
      "change_type":       "Mutate",
      "description":       "Field 'age' value changed from 30 to 31.",
      "intent_annotation": "Matches intent: 'increment age'.",
      "resolution":        null
    }
  ],
  "summary":          "2 changes required: 1 rename, 1 mutate.",
  "unresolved_count": 0
}
```

**Refinement mode** (when `prior_trace` is supplied): each `ChangeItem` gains `resolution: "resolved"` or `"unresolved"`. Only unresolved items need attention in the next script revision.

**Rate limited**: Yes.

---

## Rate Limit Exceeded Response

Applies to all tools when the sliding-window limit is hit.

```json
{
  "error":               "rate_limit_exceeded",
  "retry_after_seconds": 3,
  "message":             "Rate limit of 20 requests/minute exceeded. Retry in 3 seconds."
}
```

The `retry_after_seconds` value is the time until the oldest request in the current window expires, computed by the `SlidingWindowRateLimiter`.
