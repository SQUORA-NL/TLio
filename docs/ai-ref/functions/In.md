# in

> Returns true when a value equals any of the given options.

## Syntax

```
=in(<value>, <option1>, <option2>, ...)
=in(<value>, <arrayPath>)
```

## Arguments

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | any or path | yes | The value to look for. |
| 2..n | any, path, or array path | yes (at least one) | Options. An argument that resolves to an array contributes all its elements. |

## Returns

A boolean node. A missing value is `false`.

## Example

```json
{ "command": "ifElse",
  "condition": "=in($.status, 'gold', 'silver', 'platinum')",
  "ifScript": [{ "command": "add", "path": "$.prioritySupport", "value": true }] }
```

Options can come from the document itself:

```json
{ "condition": "=in($.status, $.config.allowedStatuses)" }
```

## When to use

- Testing one field against a set of allowed values — shorter and clearer than chained `or(equals(...))`.
- Validating against a whitelist that lives in the document or in configuration.

## When NOT to use

- Testing whether an array *contains* a value where the array is the subject — `in` takes the value first, options second.
- Substring matching — use `contains` from the Text pack.

## Common mistakes

- **Argument order**: `=in($.status, $.allowed)` — value first, options second. Reversing it silently tests the wrong thing.
- **Equality rules apply**: numbers and numeric text match each other; text matching is case-sensitive. See [equals](Equals.md).
