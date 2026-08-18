# isArray

> Returns true when a value is an array.

## Syntax

```
=isArray(<value>)
```

## Arguments

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | any or path | yes | The value to test. |

## Returns

A boolean node. An empty array is still an array (`true`); a missing path is `false`.

## Example

```json
{ "command": "ifElse",
  "condition": "=isArray($.tags)",
  "ifScript":   [{ "command": "add", "path": "$.tagCount", "value": "=count($.tags)" }],
  "elseScript": [{ "command": "add", "path": "$.tagCount", "value": 0 }] }
```

## When to use

- Handling a field that may be either a single value or a list.
- Guarding array-only operations (`merge` with array settings, `count`, `join`).

## When NOT to use

- Checking whether the array has elements — use `isEmpty` or compare `count` to 0.

## Common mistakes

- **Empty vs missing**: `[]` is an array; an absent property is not. Use `exists` to tell them apart.
- **Wildcard paths**: `$.items[*]` selects the elements, not the array itself — `=isArray($.items[*])` tests the first element.
