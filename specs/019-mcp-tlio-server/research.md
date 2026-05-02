# Research: TLio MCP Server (019)

## Decision 1 — MCP SDK for C# / .NET

**Decision**: Use the official `ModelContextProtocol` NuGet package (Anthropic C# MCP SDK).

**Rationale**: Official SDK with stdio server support and attribute-based tool registration (`[McpServerTool]`). Tools declare input schemas via C# method signatures + XML doc comments; the SDK handles JSON-RPC framing, request routing, and error serialisation automatically.

**Alternatives considered**:
- Raw JSON-RPC over `Console.In`/`Console.Out`: Too low-level; re-implements protocol handling the SDK already provides. Rejected.

**Usage pattern**:
```csharp
// Program.cs
var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddMcpServer().WithStdioServerTransport().WithToolsFromAssembly();
await builder.Build().RunAsync();

// Tool class
[McpServerToolType]
public class ExecutionTools
{
    [McpServerTool, Description("Execute a TLio script against a document.")]
    public ExecuteResult ExecuteScript(ExecuteRequest req) { ... }
}
```

---

## Decision 2 — Rate Limiting

**Decision**: `System.Threading.RateLimiting` built into .NET (via `System.Threading.RateLimiting` package, in-box from .NET 7+). Use `SlidingWindowRateLimiter` per client connection.

**Rationale**: Zero external dependency. The sliding window algorithm distributes burst traffic evenly, avoids the sharp cutoff of fixed windows, and returns a `RetryAfter` value needed by FR-014. Configurable via `McpConfiguration`.

**Configuration shape**:
```json
{ "RateLimit": { "RequestsPerMinute": 20, "WindowCount": 6 } }
```
`WindowCount`: number of sub-windows (60s / 6 = 10s segments — smooth bucket).

**Alternatives considered**:
- Custom token bucket: Unnecessary; `SlidingWindowRateLimiter` covers the requirement with zero code.
- ASP.NET Core middleware: Wrong host model (stdio, not HTTP).

---

## Decision 3 — Trace Instrumentation (Observability)

**Decision**: Add `ITraceCollector?` as a nullable property to `IExecutionContext<TNode>` in `TLio.Core`. Each command's `Execute()` method emits a structured `TraceEntry` after its existing `LogInfo`/`LogWarning`/`LogError` call, guarded by null-check.

**Rationale**:
- Additive: all existing code paths unchanged when `TraceCollector` is `null` (the default).
- Type-safe: structured record, no string parsing.
- Constitution-compliant: supplements Article X logging; no existing behaviour altered.
- Toggle: MCP server wires `McpTraceCollector` when trace is enabled; passes `null` when disabled.

**TraceEntry fields**: `CommandName`, `Path`, `Outcome` (Success / NoOp / Failure), `MatchedCount`, `Detail`.

**Change scope in TLio.Core**:
- Add `ITraceCollector.cs`, `TraceEntry.cs`, `TraceOutcome.cs` to `TLio.Core/Contracts` and `TLio.Core/Models`.
- Add `ITraceCollector? TraceCollector { get; set; }` to `IExecutionContext<TNode>`.
- Add `TraceCollector = null;` default property to `ExecutionContextBase<TNode>` (or each concrete context).

**Change scope in TLio.Commands / Extensions**:
- Each command's `Execute()` appends one `context.TraceCollector?.Record(new TraceEntry(...))` call.
- Approximately 14 command files touched (purely additive lines).

**Alternatives considered**:
- Parse `IExecutionLogger` string entries: Fragile, couples trace format to log message text. Rejected.
- Wrap `ScriptEngine<TNode>`: Too coarse — misses per-command granularity. Rejected.
- OpenTelemetry: Overkill for a local stdio tool. Rejected.

---

## Decision 4 — Discovery Tool Implementation

**Decision**: Serve `docs/ai-ref/` markdown files directly. Tool reads the relevant `.md` file and returns its content.

**Rationale**: `docs/ai-ref/` already exists with 43 well-structured files covering all commands, functions, and adapters (Article XI). Serving them directly gives zero duplication, guaranteed freshness, and no reflection complexity.

**Directory mapping**:
| Request | File served |
|---------|-------------|
| `list_commands` | Glob `docs/ai-ref/commands/*.md` — extract name + first description line |
| `list_functions` | Glob `docs/ai-ref/functions/*.md` — same |
| `describe name=Set` | `docs/ai-ref/commands/Set.md` full content |
| `describe name=concat` | `docs/ai-ref/functions/Concat.md` full content |
| `describe name=json-newtonsoft` | `docs/ai-ref/adapters/json-newtonsoft.md` full content |

**Root resolution**: The MCP server resolves paths relative to `AppContext.BaseDirectory` or a configured `AiRefRoot` environment variable, so it works regardless of working directory.

**Alternatives considered**:
- Reflection over command assemblies: duplicates ai-ref.md work, fragile with private members. Rejected.

---

## Decision 5 — Gap Analysis (Structural Diff)

**Decision**: Format-specific tree diff implemented in `TLio.Mcp` using concrete format types (JToken, XElement, YamlNode).

**Rationale**: TLio.Mcp is a tool layer and may reference adapter libraries (same pattern as TLio.Sample.Api). Format-specific diffing is more precise than a generic approach and avoids requiring new `INodeAdapter` read methods. The diff outputs path-addressed `ChangeItem` records the calling agent can map to TLio commands.

**Algorithm** (per format):
1. Flatten both documents to a `Dictionary<string, string?>` of `path → value` (leaf-only, paths expressed in the native format's path style).
2. Compute symmetric diff:
   - In input but not target → `Remove`
   - In target but not input → `Add`
   - In both but different value → `Mutate`
   - Key renamed (old key removed, structurally equivalent key added) → `Rename` heuristic (Levenshtein distance on path segments ≤ 1).
3. Array reorder detection: same elements, different positions → `Reorder`.
4. Return as ordered `ChangeItem[]`.

**Alternatives considered**:
- Generic INodeAdapter-based diff: INodeAdapter is write-focused; adding read-enumeration methods would require new interface members and implementations across all adapters. Disproportionate scope. Rejected.
- Serialize everything to JSON and diff: Lossy for XML (attributes vs child nodes). Rejected.

---

## Decision 6 — Observability Toggle

**Decision**: `appsettings.json` key `Observability:Enabled` (bool, default `true`) with environment variable override `TLIO_MCP_TRACE` (`true`/`false`).

**Rationale**: Follows standard .NET configuration provider layering — JSON file sets default, env var overrides for high-performance deployments. No redeployment required. Aligns with FR-012.

**Rate limit config lives alongside**: `RateLimit:RequestsPerMinute` (int, default `20`), `RateLimit:WindowCount` (int, default `6`).

**Alternatives considered**:
- Per-request boolean parameter: Adds API surface; spec says "disabled via configuration", not per-request. Rejected.
- Startup CLI flag: Insufficient (can't override without restart; env var achieves same with more flexibility). Rejected.

---

## Decision 7 — MCP Transport

**Decision**: Stdio (`Console.In`/`Console.Out` via `WithStdioServerTransport()`).

**Rationale**: Standard MCP convention for local tools. Works with Claude Code (`--mcp-server` flag), Claude Desktop (`mcpServers` config), and any MCP-compatible client with no network port, firewall rules, or TLS.

**Alternatives considered**: HTTP/SSE transport — future extension, out of scope per spec Assumptions.
