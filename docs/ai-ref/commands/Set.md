# set

> Sets the value of an **existing** node; logs a warning and skips if the node is absent.
> Use `put` for upsert (create-or-update) or `add` to create-only.

## Syntax

```json
{ "command": "set", "path": "$.target", "value": <TLioValue> }
```

Two-argument form (select parent, name child property):

```json
{ "command": "set", "path": "$.items[*]", "property": "active", "value": true }
```

> See [Notation Reference](../notation-reference.md) for quoting rules and escape sequences.

## Options

| Option | Type | Required | Default | Description |
|--------|------|----------|---------|-------------|
| path | string | yes | — | Selects the target node(s). With `property`: selects parent node(s). |
| property | string | no | — | Name of the child key to set on each matched parent. |
| value | TLioValue | yes | — | Literal value or `=function()` expression to assign. |

**Supports functions**: ✅

## Formats

Works with all adapters. Path syntax differs per adapter — see [overview.md](../overview.md).

## Example

```json
[
  { "command": "set", "path": "$.address.city", "value": "Amsterdam" },
  { "command": "set", "path": "$.items[*]", "property": "active", "value": true }
]
```

## When to use

- The field **definitely exists** in the document and you want to **update** its value.
- You want **fail-fast signalling**: a noop trace (with `"property not found"`) immediately tells you the path was wrong, without creating phantom fields.
- You are updating every element of an array (`$.items[*]`) where all elements already carry the target property.
- The schema is fixed and known upfront — `set` enforces that the document already conforms.

## When NOT to use

- The field may be absent — use `put` (upsert) instead; `set` will silently noop.
- You want to **create** a new field — use `add` (create-only) or `put` (upsert). `set` will **never** create a field.
- You are unsure whether the field exists — use `put` as the safe default.
- The script may run on documents of varying shape — `set` silently skips missing paths, which can hide data-quality problems; use `put` or validate with an assertion step first.

## Comparison: Add vs Set vs Put

| | `add` | `set` | `put` |
|---|---|---|---|
| Field absent | Creates it | **Noop** (logs warning) | Creates it |
| Field present | Noop (skips) | **Updates** it | Updates it |
| Array target | Appends | Replaces element | Replaces whole array |
| Idempotent re-run | Safe — never overwrites | Safe — only touches existing | Safe — always writes latest value |
| Use when | Create-only | **Update-only (field guaranteed present)** | Upsert / unsure |

**Decision rule for agents:**
- Know the field is absent and must stay absent once written → `add`
- Know the field exists and must be updated → `set`
- Don't know, or want unconditional write → `put`

## Common mistakes

- **Using `set` to create a new field**: the command silently noops; the field is never created. Switch to `add` or `put`.
- **Treating a noop trace as success**: if `set` produces `outcome: "noop"` with `"property not found"`, the field was absent. This is not an error by default — but it means your path is wrong or the document is missing the expected field.
- **Using `set` on a wildcard path where some elements lack the property**: matching elements that have the property are updated; those without it silently noop. If all elements must be updated, verify the schema or use `put`.
- **Forgetting that `set` does not create parent paths**: if the parent object is missing, the result is `"failure"`, not a noop.

## Failure modes and what the trace tells you

| Situation | Trace outcome | Trace detail | Action |
|---|---|---|---|
| Target property is absent | `noop` | contains `"property not found"` | The path is wrong or the field does not exist. Fix the path, or switch to `put` if the field should be created. |
| Parent path is missing | `failure` | path resolution error | The parent object/array does not exist. Insert an `EnsurePath` or `put` step to create it first. |
| Path resolves and field exists | `success` | — | Field was updated as expected. |

## C# Fluent API

```csharp
var script = new TLioScript<JToken>()
    .Set(JValue.CreateString("Amsterdam")).OnPath("$.address.city");
```
