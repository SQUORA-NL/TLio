# datecompare

> Compares two dates and returns -1, 0, or 1 indicating their relative order.

## Syntax

```
=datecompare(<date1>, <date2>)
```

## Arguments

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | date string or path | yes | The first date. |
| 2 | date string or path | yes | The second date. |

## Returns

A long integer:
- `-1` — date1 is before date2
- `0` — dates are equal
- `1` — date1 is after date2

**Important**: The return type is `long` (integer), not a string like "before"/"after"/"equal".

## Example

```json
{ "command": "set", "path": "$.cmp", "value": "=datecompare($.d1,$.d2)" }
```

Input: `{ "d1": "2024-03-01", "d2": "2024-06-01", "cmp": null }`
Output: `{ ..., "cmp": -1 }`

## Usage in scripts

```json
{ "command": "ifElse",
  "condition": "=datecompare($.created,$.deadline)",
  "thenPath": "$.overdue", "thenValue": true,
  "elsePath": "$.overdue", "elseValue": false }
```
