# join

> Joins array elements into a single string with a separator.

## Syntax

```
=join(<array>, <separator>)
```

## Arguments

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | array or path | yes | Path to an array of strings/values. |
| 2 | string or path | yes | The separator to insert between elements. |

## Returns

A string node.

## Example

```json
{ "command": "set", "path": "$.date", "value": "=join($.parts,'-')" }
```

Input: `{ "parts": ["2024", "01", "15"], "date": "" }`
Output: `{ ..., "date": "2024-01-15" }`
