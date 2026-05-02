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
