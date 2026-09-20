# path

> Returns the current node's absolute path as a string. Alias for `=scriptpath()`.

## Syntax

```
=path()
```

> See [Notation Reference](../notation-reference.md) for quoting rules and escape sequences.

## Options

None. Identical behaviour to `=scriptpath()`.

## Formats

Works with all adapters. Path format is adapter-specific (e.g., `$.items[0]` for JSON).

## Example

```json
{ "command": "add", "path": "$.items[*].loc", "value": "=path()" }
```

Input: `{ "items": [{ "id": 1 }, { "id": 2 }] }`
Output: `{ "items": [{ "id": 1, "loc": "$.items[0]" }, { "id": 2, "loc": "$.items[1]" }] }`

## Notes

- `=path()` and `=scriptpath()` are registered separately but share the same implementation (`ScriptPath<TNode>`).
- Use `=path()` for JLio compatibility; use `=scriptpath()` for explicit TLio naming.
- `=path()` also has a 3-argument "find" shape — `=path(*, kinds, recursive)` — that returns
  descendant *nodes* instead of a path string. See
  [ScriptPath.md#find-mode-scriptpath-kinds-recursive](ScriptPath.md#find-mode-scriptpath-kinds-recursive).

## C# Usage

```csharp
// Both are identical at runtime:
options.FunctionsProvider.Register("path",       () => new ScriptPath<JToken>());
options.FunctionsProvider.Register("scriptpath", () => new ScriptPath<JToken>());
```

## When to use

- Capturing the **absolute path of the node being processed** for logging, metadata, or audit trail fields — e.g. stamping each item in a collection with its own path.
- Self-referential scripts that operate over multiple nodes (via wildcard `path` in the command) and need to record which node was touched.
- Debugging: add a temporary `add` command with `=path()` to inspect which nodes a wildcard resolves to at runtime.
- Building path-indexed maps or lookup structures where the key is the node's position in the document.

## When NOT to use

- You need the **value at a path** — use `=fetch($.some.path)` instead. `=path()` returns the path *string* of the current context node, not the data at some other location.
- You need a static, hard-coded string — just use a literal string value; `=path()` is only useful when the path changes per node (e.g. in wildcard expansions).
- Pure data transformation with no metadata requirement — `=path()` adds overhead and produces path strings that are meaningless to downstream consumers who don't need them.

## Comparison

| Function | Returns | Use when |
|----------|---------|----------|
| `=path()` | Absolute path string of the current context node | You need to record *where* in the document you are |
| `=scriptpath()` | Same — exact alias | Prefer this name for explicit TLio naming |
| `=fetch(<path>)` | Value at the specified path | You need the data *at* a path, not the path string itself |

## Common mistakes

- **Confusing path with fetch**: `=path()` returns the *path string* of the current node in context — not the value at some path you specify. If you want a value, use `=fetch()`.
- **Calling path() with a path argument**: `=path()` takes no arguments (use `=scriptpath(@.child)` for relative sub-path resolution). Passing an argument to `=path()` is not supported.
- **Expecting an absolute path to an arbitrary node**: `=path()` always returns the path of the node currently being processed by the command, not an arbitrary node you choose. The context is set by the command's `path` field.
- **Using path() at document root**: when a command targets `$` (the root), `=path()` returns `"$"` — a single dollar sign. This is correct but may be surprising.
