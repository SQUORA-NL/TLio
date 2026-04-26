# Data Model: API Script Slug Cache

**Feature**: `018-api-script-slug-cache`  
**Date**: 2026-04-26

---

## Entities

### ScriptRegistryEntry

Represents a registered slug and its pre-compiled scripts for all supported formats.

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `Slug` | `string` | Yes | URL-safe identifier (`[a-z0-9\-_]+`); unique within the registry |
| `Source` | `string` | Yes | The original TLio script source text (JSON array); preserved for `GET /scripts` |
| `CompiledJson` | `CompiledScript<JToken>` | Yes | Pre-compiled form for JSON input |
| `CompiledXml` | `CompiledScript<XElement>` | Yes | Pre-compiled form for XML input |
| `CompiledYaml` | `CompiledScript<YamlNode>` | Yes | Pre-compiled form for YAML input |
| `RegisteredAt` | `DateTimeOffset` | Yes | UTC timestamp of last registration or replacement |

**Validation rules**:
- `Slug` must match `^[a-z0-9\-_]+$`; max length 128 characters
- `Source` must be valid JSON parseable as a TLio command array; compilation against all three adapters must succeed for the entry to be stored
- `CompiledJson`, `CompiledXml`, `CompiledYaml` are immutable after creation; each `Execute()` call internally clones commands for thread safety (`CompiledScript.CreateExecutable()`)

---

### ScriptRegistry

In-memory store of all `ScriptRegistryEntry` objects. Lifecycle matches the application process.

| Operation | Behaviour |
|-----------|-----------|
| Add | If slug absent: insert. If slug present: replace atomically. |
| Get | Thread-safe read by slug; returns null if not found. |
| List | Returns a snapshot of all entries at the moment of the call. |
| Delete | Removes entry by slug; no-op if slug absent (caller handles 404). |

**Concurrency**: Backed by `ConcurrentDictionary<string, ScriptRegistryEntry>`. Reads are lock-free. Writes (add/replace/delete) use the dictionary's atomic operations.

**State transitions**:

```
[Absent] ──register──► [Registered] ──re-register──► [Registered (updated)]
                              │
                          delete
                              │
                              ▼
                          [Absent]
```

Restart clears all runtime-registered entries. Startup-seeded entries from the config file are re-loaded on every startup.

---

### SlugRegistrationPayload

Inbound model for `POST /scripts`. Parsed from the request body regardless of content type.

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `slug` | `string` | Yes | The desired slug identifier |
| `script` | `string` | Yes | The TLio script source — a JSON array of command objects, embedded as a string |

**Parsing strategy**: The server attempts to deserialise the request body into this shape from JSON (try first), then XML, then YAML. If no format succeeds, returns HTTP 400.

---

### StartupScriptConfig

Schema of the `scripts-config.json` startup file.

| Field | Type | Description |
|-------|------|-------------|
| Root | `array` | Array of `StartupScriptEntry` objects |

**StartupScriptEntry**:

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `slug` | `string` | Yes | Slug identifier |
| `script` | `string` | Yes | TLio script source text (inline JSON array as a string) |

**Example**:
```json
[
  {
    "slug": "add-greeting",
    "script": "[{\"command\":\"set\",\"path\":\"$.greeting\",\"value\":\"Hello\"}]"
  }
]
```

---

### ScriptExecutionContext (runtime, not persisted)

Transient per-request state used during slug execution. Not stored.

| Field | Type | Description |
|-------|------|-------------|
| `Slug` | `string` | The slug being executed |
| `InputFormat` | `string` | Detected format: `json`, `xml`, `yaml` |
| `RawInput` | `string` | Raw request body |
| `ParsedInput` | `TNode` | Format-specific parsed node |
| `Result` | `TLioExecutionResult<TNode>` | Result after script execution |
| `ResponseContentType` | `string` | Content-Type for the HTTP response |

---

## Format Detection Logic

```
Request arrives at POST /run/{slug}
  ↓
Content-Type header present?
  ├─ application/json          → use JSON adapter
  ├─ application/xml, text/xml → use XML adapter
  ├─ application/yaml, text/yaml → use YAML adapter
  └─ absent / unknown
       ↓
       Try JSON parse → success → use JSON adapter
       Try XML parse  → success → use XML adapter
       Try YAML parse → success → use YAML adapter
       All fail → HTTP 400 "Cannot detect input format"
```

---

## Response Content-Type Mapping

| Adapter used at execution | Response Content-Type |
|--------------------------|----------------------|
| JSON (`JToken`) | `application/json` |
| XML (`XElement`) | `application/xml` |
| YAML (`YamlNode`) | `application/yaml` |
| Undetectable output | `application/octet-stream` |
