# =partial()

> Selects one element from a **multi-match path expression** by zero-based index. Use
> when a wildcard or recursive path returns multiple nodes and you need a specific one.

## Syntax

```
=partial(<path>)
=partial(<path>, <index>)
```

> See [Notation Reference](../notation-reference.md) for quoting rules and escape sequences.

Used as a value in any command: `"value": "=partial($.items[*])"`

## Arguments

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | string (path) | yes | Path expression expected to match multiple nodes (e.g., `$.items[*]`). |
| 2 | integer | no | Zero-based index of the result to return. Defaults to `0` (first match). |

## Returns

The node at the specified index from the match list. When the path matches nothing, or the
index is negative or `>=` the match count, `partial` logs a warning and returns a **failed**
result (not `null`) — the same failure shape as an unmatched `fetch`, so a `set`/`add` using
it typically leaves the target unwritten rather than writing a literal null.

## Verified example

Given `{ "items": ["first", "second", "third"], "result": null }`:

```json
{ "command": "set", "path": "$.result", "value": "=partial($.items[*])" }
```

Result: `{ "items": ["first", "second", "third"], "result": "first" }`

Verified by: `TLio.Functions.Tests/Fixtures/Partial/01-partial-first-element/fixture.json`

```json
{ "command": "set", "path": "$.result", "value": "=partial($.items[*], 2)" }
```

Result: `{ "items": ["first", "second", "third"], "result": "third" }`

Verified by: `TLio.Functions.Tests/Fixtures/Partial/02-partial-index-2/fixture.json`

## C# Usage

```csharp
// Already registered via ParseOptions.CreateDefault()
var options = ParseOptions<JToken>.CreateDefault();
var engine = new ScriptEngine<JToken>(options.CommandsProvider, options.FunctionsProvider);
var result = engine.Execute(
    "[{\"command\":\"set\",\"path\":\"$.pick\",\"value\":\"=partial($.items[*],1)\"}]",
    JObject.Parse("{\"items\":[\"first\",\"second\",\"third\"]}"),
    JsonExecutionContext.CreateDefault());
```

## When to use

- A wildcard or recursive path returns multiple nodes and you need one **specific element by position** — e.g. `=partial($.items[*].price, 2)` returns the price of the third item.
- The index is computed or variable and cannot be embedded in the path literal at script-write time.
- You want to extract a named property from all matched elements and then select one: `=partial($.users[*].email, 0)` picks the first user's email from a wildcard match.

## When NOT to use

- You **know the index at script-write time** and can express it directly in the path — prefer `$.items[2].price` over `=partial($.items[*].price, 2)`. The direct path is cleaner and more explicit.
- You need **all** matched values — iterate over the wildcard path directly or use array-aware commands rather than calling `partial` for each position.
- The path is guaranteed to return exactly one node — use `=fetch()` instead; `partial` is designed for multi-match scenarios.

## Comparison

| Function | Use when |
|----------|----------|
| `=partial(<path>, N)` | Wildcard/recursive path; you need element at index N |
| `$.items[N].field` (direct path) | Index is known and fixed; simpler and more readable |
| `=fetch(<path>)` | Path returns exactly one node; no index selection needed |

## Common mistakes

- **Zero-based indexing**: `partial(expr, 0)` is the **first** element, `partial(expr, 1)` is the second. Off-by-one errors are the most common mistake.
- **Out-of-bounds index**: if the path matches fewer elements than the requested index, `partial` logs a warning and fails — it does not write a literal `null`. Validate that the collection has enough elements before using a high index.
- **Using partial on a single-match path**: `partial` is for multi-match paths. On a path that returns one node, index 0 works but `=fetch()` is the more appropriate and readable choice.
- **Confusing with fetch**: `=fetch()` returns the first match of any path; `=partial()` is explicit about selecting from a multi-match result by index and communicates intent more clearly.
