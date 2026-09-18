# =toArray()

> Wraps a node in a new array, with the current value, if any, as the first (and only)
> element.

## Syntax

```
=toArray()
=toArray(<path>)
```

> See [Notation Reference](../notation-reference.md) for quoting rules and escape sequences.

Used as a value in any command: `"value": "=toArray($.tag)"`, or bare — `"value": "=toArray()"`.

## Arguments

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | string (path) | no | Path to the node to wrap. When omitted, wraps the **current node** — the node the command is presently processing — the same fallback `scriptpath()` uses when called with no argument. |

## Returns

An array. Its contents depend on the node being wrapped:

| Node | Result |
|------|--------|
| Missing (path matches nothing) | `[]` |
| `null` | `[]` |
| Already an array | A deep clone of it, unchanged — no double-wrapping |
| Anything else (scalar, object) | `[<deep clone of the node>]` |

## Example

Wrapping a value at a path:

```json
{ "command": "set", "path": "$.result", "value": "=toArray($.tag)" }
```

Given `{ "tag": "red" }` → `$.result` = `["red"]`

Wrapping the current node, over a wildcard match — each match is its own current node, so
the same call wraps every element individually:

```json
{ "command": "set", "path": "$.tags[*]", "value": "=toArray()" }
```

Given `{ "tags": ["red", "blue"] }` → `$.tags` = `[["red"], ["blue"]]`

Normalizing a field that is sometimes absent, sometimes scalar, sometimes already an array:

```json
{ "command": "set", "path": "$.tags", "value": "=toArray($.tags)" }
```

Given `{ "tags": "red" }` → `$.tags` = `["red"]`
Given `{ "tags": ["red","blue"] }` → `$.tags` = `["red","blue"]` (unchanged)
Given `{ "tags": null }` → `$.tags` = `[]`

## C# Usage

```csharp
var options = ParseOptions<JToken>.CreateDefault();
// Use in script JSON: "=toArray($.path)" or "=toArray()"
```

## When to use

- Normalizing a value that may be absent, a scalar, or already an array into a single
  always-array shape, so downstream code never has to branch on the source shape.
- Building an array field from a single existing value without knowing in advance whether a
  value is even present.
- Applied bare (`=toArray()`) over a wildcard match to wrap every matched element
  individually, one array per match.

## When NOT to use

- **Adding an element to an existing array of known length** — use `add` with a trailing
  index, or `merge`. `toArray` replaces the whole value with a fresh array; it does not append.
- **Combining several distinct paths into one array** — `toArray` takes a single source (one
  path, or the current node). To collect several matches, `sort`/`sortBy`/`distinct` operate
  on an already-assembled collection instead.
- **You need the wrapped value to always contain something even when the source is absent or
  null** — `toArray` deliberately produces `[]` for "no value yet"; pad with `coalesce` first
  if a placeholder element is required.

## Comparison

| Tool | Effect | Use when |
|------|--------|----------|
| `=toArray(<path>)` | Wraps a node in `[<node>]` | You need to guarantee an array shape around one value |
| `=promote(<path>)` | Wraps a node in `{ "key": <node> }` | You need an object layer, not an array |
| `add` with a trailing index | Appends one element to an existing array | The array already exists and you are growing it |

## Common mistakes

- **Expecting `toArray` to append**: like `promote`, `toArray` always produces a brand-new
  array value — it does not read and extend an existing array at the target path beyond
  wrapping. Combine with `add`/`merge` for growth.
- **Calling `toArray()` bare outside a wildcard match expecting it to wrap the whole
  document**: with no argument, the current node depends on how the containing command
  resolved its target — for a plain property path (`$.result`), the current node is typically
  the *parent* object, not the property's existing value. Pass an explicit path
  (`=toArray($.result)`) when the target is a single named field.
- **Assuming an already-array value gets nested**: `toArray` checks for this and returns the
  array as-is (deep-cloned) rather than producing `[[...]]`. Nesting is intentionally not the
  default.
