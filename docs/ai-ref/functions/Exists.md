# exists

> Returns true when a path matches at least one node, whatever its value.

## Syntax

```
=exists(<path>)
```

## Arguments

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | path | yes | The path to test. |

## Returns

A boolean node. A property that is present but null **exists**; a missing property does not.

## Verified example

Input (excerpt):

```json
{ "address": { "city": "Utrecht" } }
```

| Expression | Result |
|---|---|
| `=exists($.address.city)` | `true` |
| `=exists($.address.zip)` | `false` |
| `=exists($.nickname)` (present, value `null`) | `true` |
| `=exists($.missing)` | `false` |

Verified by: `PredicateFunctionTests.Existence` (`TLio.Functions.Tests/FunctionsTests/LogicTests/PredicateFunctionTests.cs:103-113`)

## When to use

- Guarding a step that would otherwise silently write nothing when the source is absent.
- Distinguishing "absent" from "present but null" — pair with `isNull`.
- Validating required fields before a transformation.

## When NOT to use

- Checking for a blank string — use `isEmpty` (an empty string exists).
- Checking a value — use `equals` or the comparison predicates.

## Common mistakes

- **Confusing exists with non-null**: `=exists($.nickname)` is true when `nickname` is `null`. Use `=not(isNull($.nickname))` for "has a real value".
- **Wildcards**: `=exists($.items[*].sku)` is true when *any* element has a sku, not when all do.
