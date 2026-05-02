# split

> Splits a delimited string into an array of substrings.

## Syntax

```
=split(<source>, <delimiter>)
```

## Arguments

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | string or path | yes | The string to split. |
| 2 | string or path | yes | The delimiter character or string. |

## Returns

A JSON array node of string values.

## Example

```json
{ "command": "set", "path": "$.tags", "value": "=split($.csv,',')" }
```

Input: `{ "csv": "java,python,csharp", "tags": null }`
Output: `{ ..., "tags": ["java", "python", "csharp"] }`
