# put

> **Upsert**: sets the value if the node already exists, creates it if absent.
> Use `set` when the node must pre-exist (silently no-ops if missing, so you can check the
> trace), or `add` to create-only (skips if exists).

## Syntax

```json
{ "command": "put", "path": "$.field", "value": <TLioValue> }
```

Two-argument form (upsert child of matched parent):

```json
{ "command": "put", "path": "$.address", "property": "country", "value": "NL" }
```

> See [Notation Reference](../notation-reference.md) for quoting rules and escape sequences.

## Options

| Option | Type | Required | Default | Description |
|--------|------|----------|---------|-------------|
| path | string | yes | — | Selects target node(s). With `property`: selects parent node(s). |
| property | string | no | — | Name of the child key to upsert on each matched parent. |
| value | TLioValue | yes | — | Literal value or `=function()` expression to assign. |

**Functions in the value**: ✅ value  
**Functions in the path**: ✅ `=indirect()` in `path`

## Formats

Works with all adapters. Path syntax differs per adapter — see [overview.md](../overview.md).

## Verified example

Creating a value that does not exist yet — the parent object(s) along the path are created too:

```json
// input
{}

// script
[ { "command": "put", "path": "$.person.address.city", "value": "Amsterdam" } ]

// result
{ "person": { "address": { "city": "Amsterdam" } } }
```

Verified by: `TLio.UnitTests/Fixtures/Put/03-put-deep-path/fixture.json`

Updating a value that already exists:

```json
// input
{ "name": "old" }

// script
[ { "command": "put", "path": "$.name", "value": "new" } ]

// result
{ "name": "new" }
```

Verified by: `TLio.UnitTests/Fixtures/Put/02-put-update-existing/fixture.json`

Writing into a `null` node upgrades it to an object instead of noop-ing — `null` is treated as
an unfilled container, the same rule `add` follows:

```json
// input
{ "customer": null }

// script
[ { "command": "put", "path": "$.customer.demo", "value": 3 } ]

// result
{ "customer": { "demo": 3 } }
```

Verified by: `TLio.Parity.Tests/Fixtures/Put/06-put-into-null-node/fixture.json`

## When to use

- You **don't know** whether the field already exists — `put` handles both cases.
- You want an **unconditional write** regardless of current state.
- The script is **replayed or run multiple times** and the latest value must always win (idempotent overwrite).
- You are migrating or normalising documents that may or may not have a given field.
- When in doubt between `add` and `set`: `put` is the safe default.

## When NOT to use

- You want to **protect an existing value** from being overwritten — use `add` instead; `put` will overwrite.
- You want **fail-fast** signalling if a field is unexpectedly absent — use `set`; a missing field will noop and log, whereas `put` silently creates it.
- The target is an **array** and you only want to append to it — `put` replaces the entire array with the new value. Use `add` to append.

## Array positions

A trailing integer subscript names a position, and `put` writes that element:

```json
{"command":"put","path":"$.items[0]","value":9}     // ["a","b"] → [9,"b"]
```

Unlike a missing *property*, a missing position is not created: an array has no holes, so `put`
warns and changes nothing. Use `add` at the next free position.

In XML the subscript sits on the item step and counts from one. See
[document-shape.md](../adapters/document-shape.md#addressing-a-position).

## Comparison: Add vs Set vs Put

| | `add` | `set` | `put` |
|---|---|---|---|
| Field absent | Creates it | Noop (logs warning) | **Creates** it |
| Field present | Noop (skips) | Updates it | **Updates** it |
| Array target | Appends | Replaces element | **Replaces whole array** |
| Idempotent re-run | Safe — never overwrites | Safe — only touches existing | **Safe — always writes latest value** |
| Use when | Create-only | Update-only (field guaranteed present) | **Upsert / unsure** |

**Decision rule for agents:**
- Know the field is absent and must stay absent once written → `add`
- Know the field exists and must be updated → `set`
- Don't know, or want unconditional write → `put`

## Common mistakes

- **Using `put` when you want append-to-array**: `put` replaces the entire array. Use `add` to push a new element.
- **Assuming `put` errors on missing parent**: it does not — `put` creates every missing object
  along the path (and the leaf) in one step, the same mechanism `add` uses, including upgrading
  a `null` node. See `TLio.UnitTests/Fixtures/Put/03-put-deep-path/fixture.json`. The one shape
  it cannot build is a path through an array position that does not exist yet — that is a no-op
  with a warning, not a failure.
- **Using `put` when you need to detect missing fields**: `put` never noops on a missing field — it just creates it. If detecting absence is important, use `set` and inspect the trace.
- **Confusing `put` with a patch/merge**: `put` replaces the target node entirely; it does not deep-merge objects.

## Failure modes and what the trace tells you

| Situation | Trace outcome | Trace detail | Action |
|---|---|---|---|
| Parent path is missing | `success` | — | `put` creates the missing parent objects (and the leaf) as part of the write; nothing to do. |
| Parent path runs through a missing array position | `noop` | contains `"could not be created"` | That shape cannot be auto-built. Add the array elements in order first, or restructure the path. |
| Field absent, parent present | `success` | — | Field was created. |
| Field present | `success` | — | Field was updated (overwritten). |

## C# Fluent API

```csharp
var script = new TLioScript<JToken>()
    .Put(JValue.CreateString("active")).OnPath("$.status");
```
