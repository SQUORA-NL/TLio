# =scriptpath()

> Returns the **absolute path** of the currently executing node as a string. Optionally
> resolves a relative sub-path from that position. A third, unrelated 3-argument shape finds
> descendant *nodes* instead — see [Find mode](#find-mode-scriptpath-kinds-recursive) below.

## Syntax

```
=scriptpath()
=scriptpath(@.child)
=scriptpath(*, kinds, recursive)
```

Used as a value in any command: `"value": "=scriptpath()"`

> See [Notation Reference](../notation-reference.md) for path and quoting rules.
> The relative path argument uses `@.` (with dot) — `@child` without the dot is not valid in JSON/YAML context.

## Arguments

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | string (relative path) | no | Relative path starting with `@.`. Resolved from the current node's absolute path. |

## Returns

A string containing the absolute JSONPath (or format-equivalent path) of the current
node, or the resolved path if an argument is provided.

## Verified example

```json
{ "command": "set", "path": "$.result", "value": "=scriptpath()" }
```

Given `{ "result": null }` → `{ "result": "$" }` (at document root)

Verified by: `TLio.Functions.Tests/Fixtures/ScriptPath/01-scriptpath-root/fixture.json`

Stamping each element of a wildcard match with its own path (via the `path` alias — see
[Find mode](#find-mode-scriptpath-kinds-recursive) below for the 3-argument shape):

```json
{ "command": "add", "path": "$.items[*].loc", "value": "=path()" }
```

Given `{ "items": [{ "id": 1 }, { "id": 2 }] }` →
`{ "items": [{ "id": 1, "loc": "$.items[0]" }, { "id": 2, "loc": "$.items[1]" }] }`

Verified by: `TLio.Functions.Tests/Fixtures/ScriptPath/04-path-alias/fixture.json`

## Find mode: `=scriptpath(*, kinds, recursive)`

A different shape under the same name: instead of returning the path of the current node, it
**finds descendant nodes** of the current node and returns them as a set of *live nodes* — not a
path string, and not a document array either (nothing is cloned or reparented), so a caller can
write straight back into each one, e.g. with `setProperties` (see
[SetProperties.md](../commands/SetProperties.md)).

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | string | yes | Reserved as `*` for now — every name at every level. Anything else is an error. |
| 2 | array of strings | yes | Which kinds of node count as a match: `'object'`, `'primitive'` (string/number/boolean) and/or `'null'`, singular or plural, case-insensitive. `'array'` is accepted but never matches — see below. |
| 3 | boolean | yes | `false`: only the current node's direct children are tested. `true`: the whole subtree is walked. |

Arrays are always **transparent containers** — walked into when `recursive` is true, but never a
match themselves, since they have no name of their own for `kinds` to test. An object that
matches `kinds` is *still* walked into when `recursive` is true — matching and descending are
independent, which is what lets a deep field like `address.city` be reached by asking for
`'primitive'` alone, without naming `address`.

```json
{ "command": "setProperties", "path": "$.customer",
  "properties": "=scriptpath(*,['primitive'],true)", "value": "=toArray()" }
```

Given `{"customer":{"id":"C-1","address":{"city":"Amsterdam"}}}`, every primitive under
`customer` — including the nested `address.city` — becomes a one-element array, while
`address` itself (an object, not in `kinds`) is left as an object.

Asking for `kinds: ['object']` instead selects complex objects rather than primitives — every
object anywhere in the subtree, at any depth. Wrapping all of them with `setProperties` in one
call has a footgun worth knowing about first: see
[SetProperties.md — turning every complex object in a tree into an array](../commands/SetProperties.md#example-turning-every-complex-object-in-a-tree-into-an-array).

Find mode has no fixture yet (it is exercised only through inline NUnit tests); the behaviour
above — `kinds` selecting independently of `recursive`, arrays as transparent containers,
returned nodes being live and writable in place — is verified by
`TLio.Functions.Tests/FunctionsTests/ScriptPathFindTests.cs`:
`NonRecursive_PrimitiveOnly_FindsOnlyDirectScalarChildren`,
`NonRecursive_NullOnly_FindsTheNullProperty`,
`Recursive_ReachesNestedObjectProperties`,
`Recursive_ArraysAreNeverThemselvesAMatch`, and
`ReturnedNodesAreLiveReferences_ReplaceWritesBackIntoTheDocument` (mutating a returned node via
`NodeAdapter.Replace` changes the original document). `NonWildcardName_Fails` and
`UnrecognisedKind_Fails` cover the two validation errors.

## Notes

- Also registered as `"path"` (camelCase alias, 008+) for JLio compatibility — see [Path.md](Path.md).
- `=path()` and `=scriptpath()` are identical at runtime, including the find-mode shape.

## C# Usage

```csharp
// Already registered via ParseOptions.CreateDefault() under both "scriptpath" and "path"
var options = ParseOptions<JToken>.CreateDefault();
var engine = new ScriptEngine<JToken>(options.CommandsProvider, options.FunctionsProvider);
var result = engine.Execute(
    "[{\"command\":\"add\",\"path\":\"$.items[*].loc\",\"value\":\"=scriptpath()\"}]",
    JObject.Parse("{\"items\":[{\"id\":1},{\"id\":2}]}"),
    JsonExecutionContext.CreateDefault());
```

## Performance

The default (path-string) shape does one `GetPath`/`SelectNodes` lookup — the same
per-selection cost as any other path resolution in the engine (see `fetch`'s Performance
note and `EXECUTION_CONCURRENCY_INVESTIGATION.md` §6 for the adapter-dependent cost of that
lookup, worse on the System.Text.Json adapter than on Newtonsoft).

Find mode (`recursive: true`) is the one genuinely expensive shape here: `CollectChildren`
walks the **entire subtree** under the current node, visiting every object, array and scalar
once, regardless of how many actually match `kinds` — cost scales with subtree size, not
result size. There is no dedicated benchmark for find mode itself, but the underlying
per-selection cost it shares with every other path lookup is bounded in
`TLio.Xml.Tests/Performance/XmlPath_PerformanceTests.cs` and
`TLio.Yaml.Tests/Performance/YamlPath_PerformanceTests.cs` (allocations kept under ~128KB/16KB
per 1,000 iterations respectively, and a ~500-row/20-field document queried within 2 seconds
and 50MB). Call find mode once per document region you need to touch, not once per node —
`setProperties` already takes the *set* it returns and writes to all of them in one pass, so
there is no need to re-run `scriptpath(*, …)` per target.

## When to use

- Stamping each node in a wildcard expansion with its own absolute path — e.g. adding a `"_path"` audit field to every item in an array.
- Logging or metadata: recording which node was processed so downstream systems can trace transformations back to their source position.
- Self-referential scripts: when a script operates over a dynamic set of nodes and each node needs to carry its own address.
- Using the optional `@.child` argument to compute the absolute path of a sibling or child of the current node without knowing the index at script-write time.

## When NOT to use

- You need the **value at a path** — use `=fetch($.some.path)`. `=scriptpath()` returns the path *string* of the current context node, not data at some arbitrary location.
- The command targets a single, fixed node (no wildcard) — `=scriptpath()` will always return the same string; a literal is clearer.
- Pure data transformation with no audit or metadata requirement — path strings are only useful to systems that consume them.

## Comparison

| Function | Returns | Use when |
|----------|---------|----------|
| `=scriptpath()` | Absolute path string of the current context node | Canonical TLio name; use for new scripts |
| `=path()` | Same — exact alias | Use for JLio compatibility |
| `=fetch(<path>)` | Value at the specified path | You need data, not a path string |

## Common mistakes

- **Confusing scriptpath with fetch**: `=scriptpath()` returns the path *string* of the node the command is currently processing — not the value of a path you specify. For values, use `=fetch()`.
- **Expecting scriptpath to accept an arbitrary absolute path**: the optional argument is a *relative* path (`@.child`) resolved from the current node. It cannot point to an unrelated part of the document.
- **Using `@child` without the dot**: the relative path argument requires `@.` (with dot separator). `@child` is not a valid relative path in TLio's JSON/YAML notation.
- **Using scriptpath() at document root**: when a command targets `$`, `=scriptpath()` returns `"$"`. This is correct but can be surprising in scripts that expect a longer path.
- **Expecting scriptpath and path to differ**: they are exact aliases sharing the same implementation. Choose based on naming preference, not expected behaviour differences.
