# count

> Counts the number of elements in an array or matching nodes at a path.

## Syntax

```
=count(<path>)
```

## Arguments

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | array or path | yes | Path to the array or collection to count. |

## Returns

A long (integer) node with the element count.

## Example

```json
{ "command": "set", "path": "$.n", "value": "=count($.tags)" }
```

Input: `{ "tags": ["a", "b", "c"], "n": 0 }`
Output: `{ "tags": [...], "n": 3 }`
