# padright

> Right-pads a string to a specified total width with a fill character.

## Syntax

```
=padright(<source>, <width>, <padChar>)
```

## Arguments

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | string or path | yes | The string to pad. |
| 2 | integer or path | yes | Total width of the output string. |
| 3 | string or path | yes | Single character used for padding. |

## Returns

A string node padded on the right to the specified width.

## Example

```json
{ "command": "set", "path": "$.padded", "value": "=padright($.id,6,'*')" }
```

Input: `{ "id": "42", "padded": "" }`
Output: `{ ..., "padded": "42****" }`

## When to use

- Formatting output for fixed-width file formats or display columns that require left-alignment (label columns, name fields).
- Padding short codes on the right to fill a fixed-width field in a flat-file export.
- Adding trailing fill characters when content is left-aligned and the right side needs filling.

## When NOT to use

- You want zero-padded numeric IDs or right-aligned values — use `padleft` instead.
- The string is already at or beyond the target width — `padright` returns the string unchanged (no truncation). If truncation is needed, apply `substring` before padding.
- You need variable-width output — padding is only useful when a fixed width is a requirement.
- The input may be null — `padright` will fail on null source; convert to a default string first.

## Comparison

| Function | Pads side | Typical use |
|----------|-----------|-------------|
| `padright` | Right (trailing chars) | Left-aligned values: labels, display names |
| `padleft` | Left (leading chars) | Right-aligned values: numeric IDs, codes |

## Common mistakes

- **padChar must be exactly 1 character**: passing `'**'` or `''` as the pad character will fail or produce unexpected results. Always use a single character.
- **Width is total, not additional**: `padright('Hi', 6, '-')` produces `"Hi----"` (total 6 chars), not `"Hi--------"`. The width is the final string length.
- **No truncation**: if the source string is already longer than the target width, `padright` returns it unchanged. Truncate first with `substring` if needed.
- **Path resolution**: arguments resolve against the document root (dataContext). `@.field` inside a function refers to the ROOT, not a parent element.
- **Wildcard paths**: `$.items[*].label` as first argument produces a flat list. Use indexed paths for per-element operations.
- **Numeric source**: if `$.id` is a number node, it is coerced to its string representation first, then padded.
