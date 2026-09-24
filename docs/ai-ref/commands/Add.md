# add

> Creates a new property or appends to an array; **skips silently if the property already
> exists**. Use `put` to update existing values, or `set` when the node must already exist.

## Syntax

```json
{ "command": "add", "path": "$.newProp", "value": <TLioValue> }
```

Two-argument form (add child to matched parent):

```json
{ "command": "add", "path": "$.address", "property": "country", "value": "NL" }
```

> See [Notation Reference](../notation-reference.md) for quoting rules and escape sequences.

## Options

| Option | Type | Required | Default | Description |
|--------|------|----------|---------|-------------|
| path | string | yes | — | Target path for the new node. With `property`: selects parent node(s). |
| property | string | no | — | Name of the child key to create on each matched parent. |
| value | TLioValue | yes | — | Literal value or `=function()` expression to assign. |

**Functions in the value**: ✅ value  
**Functions in the path**: ✅ `=indirect()` in `path`

## Formats

Works with all adapters. Path syntax differs per adapter — see [overview.md](../overview.md).

## Verified example

Creating a new top-level property:

```json
// input
{ "existing": "value" }

// script
[ { "command": "add", "path": "$.newProp", "value": "added" } ]

// result
{ "existing": "value", "newProp": "added" }
```

Verified by: `TLio.UnitTests/Fixtures/Add/01-add-new-property/fixture.json`

`add` creates missing parent objects along the way — `$.a` already exists but is empty, and
the path is built through it to the leaf:

```json
// input
{ "a": {} }

// script
[ { "command": "add", "path": "$.a.newProp", "value": "deep" } ]

// result
{ "a": { "newProp": "deep" } }
```

Verified by: `TLio.UnitTests/Fixtures/Add/03-add-deep-path/fixture.json`

The same path-creation reaches through several missing levels at once, and through a `null`
node — `null` is treated as an unfilled container, not a value to preserve:

```json
// input
{ "a": null }

// script
[ { "command": "add", "path": "$.a.b.c", "value": 3 } ]

// result
{ "a": { "b": { "c": 3 } } }
```

Verified by: `TLio.Parity.Tests/Fixtures/Add/10-add-deep-through-null/fixture.json`

## When to use

- You want to **create a field that must not already exist** — `add` protects existing values by design.
- The script may be **replayed or run multiple times**; on re-runs the existing value is left untouched (idempotent create).
- You are populating a document template and want to guarantee initial values are only written once.
- You are appending to an array.

## When NOT to use

- The field might already exist and you want to **overwrite** it — use `put` instead.
- You need to **update** a field that should already be present — use `set` instead.
- You are unsure whether the field exists — use `put` (the safe default for unconditional write).

## Array positions

A trailing integer subscript names a position. `add` creates the element at the **next free**
position — the one place a new element goes without leaving a hole:

```json
{"command":"add","path":"$.tags[0]","value":"frontend"}   // [] → ["frontend"]
{"command":"add","path":"$.tags[1]","value":"safari"}     // → ["frontend","safari"]
```

So an array can be filled in order, one command at a time. `add` also creates the array itself
when it is missing, the same way it creates the objects along `$.address.city`.

| Position | What happens |
|---|---|
| Already occupied | skipped, warns — `add` never overwrites |
| The next free one | appended |
| Further out | no-op, warns — the element would land at an index the path did not name |

In XML the subscript sits on the item step and counts from one, so the first element is
`path: "/order/tags/item[1]"`. See
[document-shape.md](../adapters/document-shape.md#addressing-a-position).

## Comparison: Add vs Set vs Put

| | `add` | `set` | `put` |
|---|---|---|---|
| Field absent | **Creates** it | Noop (logs warning) | **Creates** it |
| Field present | **Noop** (skips) | **Updates** it | **Updates** it |
| Array target | Appends | Replaces element | Replaces whole array |
| Idempotent re-run | Safe — never overwrites | Safe — only touches existing | Safe — always writes latest value |
| Use when | Create-only | Update-only (field guaranteed present) | Upsert / unsure |

**Decision rule for agents:**
- Know the field is absent and must stay absent once written → `add`
- Know the field exists and must be updated → `set`
- Don't know, or want unconditional write → `put`

## Common mistakes

- **Using `add` to update a value**: the command will silently skip; trace outcome is `"noop"`. Switch to `put`.
- **Expecting an error when the field exists**: `add` never errors on duplicates — it just skips. If you need a hard failure, check the trace outcome instead.
- **Assuming a missing parent path fails**: it does not — `add` builds the parent objects along
  the path itself (the same mechanism `put` uses), including upgrading a `null` node into an
  object it can write into. See `TLio.UnitTests/Fixtures/Add/03-add-deep-path/fixture.json` and
  `TLio.Parity.Tests/Fixtures/Add/10-add-deep-through-null/fixture.json`. The one path shape
  `add` cannot build is one that runs through an array position that does not exist yet
  (e.g. `$.items[2].name` when `items` has fewer than 3 elements) — that is a no-op with a
  warning, not a failure.
- **Confusing `add` with array index writes**: targeting `$.items[2]` with `add` when index 2 already exists will be treated as existing → noop. Use `put` to overwrite a specific index.

## Failure modes and what the trace tells you

| Situation | Trace outcome | Trace detail | Action |
|---|---|---|---|
| Field already exists | `noop` | contains `"already exists"` | Not an error — field was previously set. Use `put` if overwrite is intended. |
| Parent path is missing | `success` | — | `add` creates the missing parent objects (and the leaf) as part of the write; nothing to do. |
| Parent path runs through a missing array position | `noop` | contains `"could not be created"` | That shape cannot be auto-built. Add the array elements in order first, or restructure the path. |
| Path resolves normally and field is absent | `success` | — | Field was created as expected. |

## C# Fluent API

```csharp
var script = new TLioScript<JToken>()
    .Add(JValue.CreateString("created")).OnPath("$.newField");
```
