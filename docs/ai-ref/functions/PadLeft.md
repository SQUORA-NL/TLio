# padleft

> Left-pads a string to a specified total width with a fill character.

## Syntax

```
=padleft(<source>, <width>, <padChar>)
```

## Arguments

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | string or path | yes | The string to pad. |
| 2 | integer or path | yes | Total width of the output string. |
| 3 | string or path | yes | Single character used for padding. |

## Returns

A string node padded on the left to the specified width.

## Example

```json
{ "command": "set", "path": "$.padded", "value": "=padleft($.id,6,'0')" }
```

Input: `{ "id": "42", "padded": "" }`
Output: `{ ..., "padded": "000042" }`

## When to use

- Generating fixed-width numeric identifiers, invoice numbers, or codes (e.g., `"000042"`).
- Zero-padding integers for lexicographic sort correctness.
- Formatting output for fixed-width file formats or display columns that require right-alignment.
- Left-padding is the most common padding direction — use it when the meaningful content is on the right and padding fills the left.

## When NOT to use

- You want padding on the right side — use `padright` instead.
- The string is already at or beyond the target width — `padleft` returns the string unchanged (no truncation). If truncation is needed, apply `substring` before padding.
- You need variable-width output — padding is only useful when a fixed width is a requirement.
- The input may be null — `padleft` will fail on null source; convert to a default string first.

## Comparison

| Function | Pads side | Typical use |
|----------|-----------|-------------|
| `padleft` | Left (leading chars) | Right-aligned values: numeric IDs, codes |
| `padright` | Right (trailing chars) | Left-aligned values: labels, display names |

## Common mistakes

- **padChar must be exactly 1 character**: passing `'00'` or `''` as the pad character will fail or produce unexpected results. Always use a single character.
- **Width is total, not additional**: `padleft('42', 6, '0')` produces `"000042"` (total 6 chars), not `"42000000"`. The width is the final string length.
- **No truncation**: if `$.id` is `"1234567"` (7 chars) and width is `6`, the result is `"1234567"` unchanged — padleft never truncates. Truncate first with `substring` if needed.
- **Path resolution**: arguments resolve against the document root (dataContext). `@.field` inside a function refers to the ROOT, not a parent element.
- **Wildcard paths**: `$.items[*].id` as first argument produces a flat list. Use indexed paths for per-element operations.
- **Numeric source**: if `$.id` is a number node, it is coerced to its string representation first (e.g., `42` → `"42"`), then padded.
