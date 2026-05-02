# abs

> Returns the absolute value of a number.

## Syntax

```
=abs(<value>)
```

## Arguments

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | number or path | yes | The value to take the absolute of. |

## Returns

A numeric node equal to `Math.Abs(value)`.

## Example

```json
{ "command": "set", "path": "$.ab", "value": "=abs($.neg)" }
```

Input: `{ "neg": -5, "ab": 0 }`
Output: `{ "neg": -5, "ab": 5 }`

## When to use

- You need to **remove the sign** before comparing or displaying a value — differences, deviations, deltas.
- You have a value that may be positive or negative and you need its magnitude.
- Preprocessing before passing to another function: `=round(abs($.delta))`.

## When NOT to use

- The value is always non-negative — `abs` is harmless but unnecessary.
- You want to **clamp** a value to a range — `abs` only removes the sign; it does not cap a maximum. Use conditional commands for clamping.
- The value might not exist — `abs` fails if the path does not resolve.

## Common mistakes

- **Path not found = failure** — if the path does not resolve, the command fails, not returns 0.
- **Using `abs` to validate non-negativity** — `abs` always returns a non-negative value, so it cannot tell you whether the original was negative. Use a conditional check if you need to detect negative input.
- **Wrong path scope** — path arguments resolve against the document root, not the current node.
