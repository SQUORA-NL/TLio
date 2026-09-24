# =promote()

> Wraps the matched node in a new object using either the node's own **property name**
> or an **explicit name** as the key.

## Syntax

```
=promote(<path>)
=promote(<path>, <propertyName>)
```

> See [Notation Reference](../notation-reference.md) for quoting rules and escape sequences.

Used as a value in any command: `"value": "=promote($.person)"`

## Arguments

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | string (path) | yes | Path to the node to promote. |
| 2 | string | no | Explicit key name for the wrapper object. When omitted, uses the node's parent property name. |

## Returns

An object with one key whose value is the matched node.

## Verified example

Using parent property name (1-arg):

```json
{ "command": "set", "path": "$.result", "value": "=promote($.person)" }
```

Given `{ "person": { "name": "Alice", "age": 30 }, "result": null }` →
`{ "person": { "name": "Alice", "age": 30 }, "result": { "person": { "name": "Alice", "age": 30 } } }`

Verified by: `TLio.Functions.Tests/Fixtures/Promote/01-promote-nested-object/fixture.json`

Using explicit name (2-arg):

```json
{ "command": "add", "path": "$.wrapped", "value": "=promote($.rawValue,'data')" }
```

Given `{ "rawValue": 42 }` → `{ "rawValue": 42, "wrapped": { "data": 42 } }`

Verified by: `TLio.Functions.Tests/Fixtures/Promote/02-promote-with-name/fixture.json`

## C# Usage

```csharp
var options = ParseOptions<JToken>.CreateDefault();
// Use in script JSON: "=promote($.path,'wrapperKey')"
```

## When to use

- Adding a **containing object layer** around a value to match an expected schema — e.g. an API expects `{ "amount": { "value": 42 } }` but the source has `{ "value": 42 }`.
- Restructuring flat data by nesting it under a named key without losing the original structure.
- Generating envelope objects: wrapping a payload in a typed container (`{ "event": <payload> }`, `{ "result": <node> }`).
- When the wrapper key name is the same as the source property name and the 1-arg form can derive it automatically.

## When NOT to use

- **Simple property renaming** — if you only want to change a key name without adding a layer, use `copy` (to the new key) followed by `remove` (of the old key). `promote` always adds a wrapping layer; it does not rename in place.
- **Merging into an existing parent** — if the target object already exists and you want to add properties to it, use the `merge` command. `promote` always creates a new single-key wrapper object.
- **Flattening or unwrapping** — `promote` only adds layers, never removes them. Use `copy` or path-based access to unwrap.

## Comparison

| Tool | Effect | Use when |
|------|--------|----------|
| `=promote(<path>)` | Wraps node in `{ "key": <node> }` | You need to add a containing object layer |
| `=toArray(<path>)` | Wraps node in `[ <node> ]` | You need an array layer, not an object layer — see [ToArray.md](ToArray.md) |
| `copy` + `remove` | Moves node to a new key at the same level | You need to rename a property (no extra layer) |
| `merge` command | Merges properties into an existing object | You need to combine nodes into one existing object |

## Common mistakes

- **Confusing promote with rename**: `promote` **adds a wrapping layer** — the original key (or the explicit name) becomes the outer key and the matched node is nested inside. It does not remove the source node or rename it in place. The result has one more level of nesting than before.
- **Omitting the explicit name when the source has no meaningful property name**: if the path points to an array element (e.g. `$.items[0]`), the auto-derived name may be the array index or undefined. Pass an explicit name to avoid unexpected results.
- **Expecting promote to modify the source**: `promote` returns a new wrapper object as a value — it does not change the node at the source path. Use `remove` separately if you want to delete the source after promoting.
- **Nesting promote inside a set targeting the same node**: writing back to the same path as the source while promoting can cause unexpected double-wrapping. Use a separate target path.
