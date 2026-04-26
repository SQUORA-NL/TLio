# Research: API Script Slug Cache

**Feature**: `018-api-script-slug-cache`  
**Date**: 2026-04-26  
**Status**: Complete — all NEEDS CLARIFICATION resolved

---

## Decision 1: Pre-compilation strategy

**Decision**: Compile the script for all three supported formats (JSON, XML, YAML) at registration time, storing one `CompiledScript<TNode>` per format in the registry entry.

**Rationale**: `TLio.Client.ScriptEngine<TNode>.Compile(scriptText, adapter)` is already available and produces a format-specific `CompiledScript<TNode>`. Because `TNode` is format-specific, there is no single generic pre-compiled form — the compilation must bind to an adapter. Compiling for all three formats at registration ensures zero script-parsing overhead on every execution path regardless of input format. The 3× compilation cost at registration is acceptable (spec SC-002 allows up to 500ms).

**Alternatives considered**:
- Compile only for JSON and convert XML/YAML at execution time → rejected: adds format-conversion overhead on every request, violates the zero-parsing-overhead goal.
- Compile on first use (lazy, per format) → rejected: first request for an unfamiliar format would incur parsing overhead, breaking SC-001.
- Store raw script + parse at execution → rejected: directly violates FR-003.

---

## Decision 2: Registry storage type

**Decision**: `ConcurrentDictionary<string, ScriptRegistryEntry>` where `ScriptRegistryEntry` holds the slug, source text, and three typed compiled fields (`CompiledScript<JToken>`, `CompiledScript<XElement>`, `CompiledScript<YamlNode>`).

**Rationale**: `ConcurrentDictionary` provides thread-safe reads with no locking and atomic add/update/remove — exactly the access pattern needed (many concurrent reads, infrequent writes). Storing typed compiled scripts avoids boxing/unboxing on the hot execution path.

**Alternatives considered**:
- `Dictionary` + `ReaderWriterLockSlim` (as used by DockerPlugin's `PluginCatalogService`) → viable but more boilerplate; `ConcurrentDictionary` is simpler for this case.
- Single compiled format with runtime conversion → rejected (see Decision 1).

---

## Decision 3: Format detection at execution time

**Decision**: Detect input format from the HTTP `Content-Type` request header. Supported values: `application/json` (→ JSON), `application/xml` or `text/xml` (→ XML), `application/yaml` or `text/yaml` (→ YAML). If header is absent or unrecognised, attempt JSON first, then XML, then YAML; if all fail, return HTTP 400.

**Rationale**: `Content-Type` is the standard HTTP mechanism for declaring body format. Fallback auto-detection handles clients that omit the header.

**Alternatives considered**:
- Require explicit `?format=json` query parameter → rejected: non-standard, adds friction for callers.
- Always require `Content-Type` header, no fallback → rejected: overly strict for a sample.

---

## Decision 4: Response Content-Type

**Decision**: Set response `Content-Type` based on the adapter used for execution (which is the same format as the input). If the script transforms data into a different format, the adapter's serialisation determines the output type. If the output cannot be classified, default to `application/octet-stream`.

**Rationale**: Per clarification — the script's output determines the content type, not the input. Because the same adapter serialises the result, the output format naturally matches the data model the script was run against. Callers that need a different output format must register a separate slug.

---

## Decision 5: Startup configuration file structure

**Decision**: A JSON file containing an array of `{ "slug": "...", "script": "..." }` objects, where `script` is the TLio script source text embedded inline (a JSON array serialised as a string, or an inline JSON array). Located at `scripts-config.json` next to the application binary, or pointed to by the `TLIO_SCRIPTS_CONFIG` environment variable.

**Rationale**: Per clarification — scripts are inline in the config (no file-path references). JSON is consistent with the existing sample infrastructure and the runtime registration payload format.

---

## Decision 6: Article X (Logging) scope

**Decision**: Article X ("every Execute() path calls LogInfo on success") applies to `TLio.Core` / `TLio.Commands` / `TLio.Functions`. The sample-layer HTTP handler follows the spec's logging rule: log registry mutations always; log execution failures only; do not add per-request success logs at the HTTP handler layer. The internal `IExecutionLogger` still captures execution trace within `CompiledScript.Execute()`.

**Rationale**: No constitutional violation — the two logging requirements operate at different abstraction layers.

---

## Existing API surface used

| Type | Location | Usage |
|------|----------|-------|
| `ScriptEngine<TNode>` | `TLio.Client/ScriptEngine.cs` | `Compile(scriptText, adapter)` at registration |
| `CompiledScript<TNode>` | `TLio.Client/CompiledScript.cs` | Stored in registry; `Execute(data, context)` at request time |
| `JsonExecutionContext.CreateDefault()` | `TLio.Json` | Creates JSON execution context |
| `XmlExecutionContext.CreateWithNativeXPath()` | `TLio.Xml` | Creates XML execution context |
| `YamlExecutionContext.CreateDefault()` | `TLio.Yaml` | Creates YAML execution context |
| `INodeAdapter<TNode>.Parse(string)` | `TLio.Core` | Parses request body to TNode |
| `INodeAdapter<TNode>.Serialize(TNode)` | `TLio.Core` | Serialises result to string |
