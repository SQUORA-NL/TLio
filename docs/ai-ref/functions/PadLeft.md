# padleft

> Left-pads a string to a specified total width with a fill character.

## Syntax

```
=padleft(<source>, <width>)
=padleft(<source>, <width>, <padChar>)
```

## Arguments

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | string or path | yes | The string to pad. |
| 2 | integer or path | yes | Total width of the output string. |
| 3 | string or path | no | Pad character. Defaults to a space `' '` when omitted. If a multi-character string is passed, only its **first** character is used — the call does not fail. |

## Returns

A string node padded on the left to the specified width.

## Verified example

Default padding (space):

```json
{ "command": "put", "path": "$.result", "value": "=padleft($.str, $.width)" }
```

Input: `{ "str": "42", "width": 5, "pad": "0" }`
Output: `{ ..., "result": "   42" }`

With an explicit pad character:

```json
{ "command": "put", "path": "$.result", "value": "=padleft($.str, $.width, $.pad)" }
```

Same input → `{ ..., "result": "00042" }`

Verified by: `TLio.Functions.Tests/Fixtures/Text/padleft/01-default-space.json` and
`02-with-char.json`.

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

- **A multi-character padChar is not an error**: `padleft('42', 5, '00')` produces `"00042"` —
  only the **first** character of the third argument is used; the call does not fail. If you
  intended `"0000042"` you need a different approach (repeat width computation), not a longer
  pad string.
- **An empty-string padChar does fail**: `padleft('42', 5, '')` fails and logs an error (there is
  no character to take), unlike a multi-character string. Omit the argument entirely for the
  space default instead of passing `''`.
- **Width is total, not additional**: `padleft('42', 6, '0')` produces `"000042"` (total 6 chars), not `"42000000"`. The width is the final string length.
- **No truncation**: if `$.id` is `"1234567"` (7 chars) and width is `6`, the result is `"1234567"` unchanged — padleft never truncates. Truncate first with `substring` if needed.
- **Path resolution**: arguments resolve against the document root (dataContext). `@.field` inside a function refers to the ROOT, not a parent element.
- **Wildcard paths**: `$.items[*].id` as first argument produces a flat list. Use indexed paths for per-element operations.
- **Numeric source**: if `$.id` is a number node, it is coerced to its string representation first (e.g., `42` → `"42"`), then padded.
