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

## Example

```json
[
  { "command": "add", "path": "$.newField", "value": "created" },
  { "command": "add", "path": "$.items", "value": ["first"] }
]
```

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
- **Missing parent path**: if the parent object (`$.address` for `$.address.country`) does not exist, the result is `"failure"`, not a silent skip. Add an `EnsurePath` step first.
- **Confusing `add` with array index writes**: targeting `$.items[2]` with `add` when index 2 already exists will be treated as existing → noop. Use `put` to overwrite a specific index.

## Failure modes and what the trace tells you

| Situation | Trace outcome | Trace detail | Action |
|---|---|---|---|
| Field already exists | `noop` | contains `"already exists"` | Not an error — field was previously set. Use `put` if overwrite is intended. |
| Parent path is missing | `failure` | path resolution error | The parent object/array does not exist. Insert an `EnsurePath` or `add`/`put` step to create it first. |
| Path resolves normally and field is absent | `success` | — | Field was created as expected. |

## C# Fluent API

```csharp
var script = new TLioScript<JToken>()
    .Add(JValue.CreateString("created")).OnPath("$.newField");
```
