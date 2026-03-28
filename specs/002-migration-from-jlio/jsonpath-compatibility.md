# JsonPath Compatibility: JsonCons vs Newtonsoft

This document records known behavioral differences between the two JsonPath engines
used in TLio and how each is handled by the corresponding adapter.

---

## Engine summary

| Adapter | Library | Node type |
|---|---|---|
| `TLio.Json` (Newtonsoft) | `Newtonsoft.Json` + `Newtonsoft.Json.Schema` JsonPath | `JToken` |
| `TLio.Json.SystemText` | `JsonCons.JsonPath 1.1.0` over `System.Text.Json` | `JsonNode` |

---

## Known differences

### 1. JSON null representation

**Newtonsoft**: JSON `null` is a real C# object — `JValue.CreateNull()` — with a
`JTokenType.Null` type and a live parent reference. `GetProperty` on a JSON-null-valued
property returns a non-null `JToken`.

**System.Text.Json**: JSON `null` IS C# `null`. `JsonObject["key"]` for a JSON-null
property returns `null`. `JsonValue.Create<object?>(null)` also returns `null`.

**Consequence**: Any generic code that calls `INodeAdapter.GetProperty` and compares
the result to `null` must distinguish "property missing" from "property exists with null
value". `PropertyChangeCommand.ReplaceProperty` uses `HasProperty` before `GetProperty`
for this reason.

**Impact on `CreateNull()`**: `SystemTextJsonNodeAdapter.CreateNull()` returns C# `null`
(not a node object). Callers of `CreateNull()` must handle a null return.
`SetProperty` and `AppendToArray` accept null values (JSON null → C# null assignment
to `JsonObject`/`JsonArray`).

---

### 2. Node parent constraint

**Newtonsoft**: `JToken` nodes can be attached to multiple parents simultaneously
(the library silently clones when needed).

**System.Text.Json**: `JsonNode` enforces single-parent ownership. Assigning a node
to a second parent throws `InvalidOperationException: The node already has a parent`.

**Consequence**: `SystemTextJsonNodeAdapter.Replace`, `SetProperty`, `AppendToArray`,
and `InsertIntoArray` all clone the incoming value when it already has a parent.
`FixedValue<JsonNode>` nodes are safe across multiple calls because Replace always
deep-clones the replacement.

---

### 3. SelectNodes path navigation strategy

**Newtonsoft** (`JsonPathItemsFetcher`): Evaluates JsonPath directly on the live
`JToken` tree; returned nodes carry their parent references natively.

**System.Text.Json** (`SystemTextJsonPathItemsFetcher`): `JsonCons.JsonPath` operates
on immutable `JsonElement` (read from `JsonDocument`), not `JsonNode`. Strategy:
1. Serialize `JsonNode` tree → JSON string.
2. Parse into `JsonDocument` and run `JsonSelector.SelectNodes`.
3. For each match, extract the `NormalizedPath` (sequence of Root/Name/Index components).
4. Replay that path on the **original** `JsonNode` tree to return live, mutable nodes.

This preserves parent references required by `Replace` and `RemoveFromParent`.

---

### 4. GetPath() output

**Newtonsoft**: Path built by prepending `$` to `JToken.Path` (e.g. `$.a.b`).

**System.Text.Json**: `JsonNode.GetPath()` (available from .NET 8+) returns the
same format: `$`, `$.a`, `$.a[0]`. Output is identical for all paths exercised
by the compliance tests.

---

### 5. Recursive-descent path order

Both engines return `$..prop` matches in document order (depth-first, left to right).
No difference observed in the compliance tests.

---

### 6. JsonPath filter expressions (`[?(...)]`)

Not tested in the current compliance suite. JsonCons fully supports RFC 9535 filter
expressions; Newtonsoft uses its own filter syntax. Mixed-use scripts that contain
filter expressions may behave differently across adapters.

---

## Compliance test status

All 33 fixture-based compliance tests in `SystemTextFixtureTests` pass against
`SystemTextJsonExecutionContext.CreateDefault()`.

Tested command/function categories:
`Add`, `Set` (including recursive-descent and null), `Put`, `Remove`,
`Copy`, `Move`, `IfElse`, `Merge`, `Compare`,
`Fetch`, `Indirect`, `Partial`, `Promote`, `ScriptPath`.
