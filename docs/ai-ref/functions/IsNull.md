# isNull

> Returns true when a value is null, or when the path matches nothing.

## Syntax

```
=isNull(<value>)
```

## Arguments

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | any or path | yes | The value to test. |

## Returns

A boolean node.

## Example

```json
{ "command": "ifElse",
  "condition": "=isNull($.middleName)",
  "ifScript":   [{ "command": "add", "path": "$.fullName", "value": "=concat($.first, ' ', $.last)" }],
  "elseScript": [{ "command": "add", "path": "$.fullName", "value": "=concat($.first, ' ', $.middleName, ' ', $.last)" }] }
```

## When to use

- Choosing a fallback when an optional field is not filled in.
- Null-guarding before a function that requires a value.

## When NOT to use

- Telling absent apart from present-and-null — use `exists`.
- Testing for a blank string — `=isNull('')` is false; use `isEmpty`.

## Comparison

| Field state | `exists` | `isNull` | `isEmpty` |
|---|---|---|---|
| missing | false | true | error (path not found) |
| `null` | true | true | true |
| `""` | true | false | true |
| `"x"` | true | false | false |

## Common mistakes

- **Assuming isNull means missing**: it covers both null and missing on purpose.
- **Using it on arrays**: an empty array is not null. Use `isEmpty` or `=equals(count($.arr), 0)`.
