# isdatebetween

> Returns true when a date falls within a range (inclusive on both ends).

## Syntax

```
=isdatebetween(<date>, <from>, <to>)
```

## Arguments

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | date string or path | yes | The date to test (ISO-8601 or `yyyy-MM-dd`). |
| 2 | date string or path | yes | Start of the range (inclusive). |
| 3 | date string or path | yes | End of the range (inclusive). |

## Returns

A boolean node: `true` if `from <= date <= to`, `false` otherwise.

## Example

```json
{ "command": "set", "path": "$.result", "value": "=isdatebetween($.event,$.from,$.to)" }
```

Input: `{ "event": "2024-06-15", "from": "2024-01-01", "to": "2024-12-31", "result": null }`
Output: `{ ..., "result": true }`
