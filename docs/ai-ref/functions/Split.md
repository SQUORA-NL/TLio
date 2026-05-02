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

## When to use

- You have a delimited string (CSV field, pipe-separated codes, space-separated words) and need to work with its parts as an array.
- You want to produce an array that can then be counted, further iterated, or re-joined with a different separator via `join`.
- You are normalising inbound data from an external system that encodes multiple values in a single string field.

## When NOT to use

- You need only a specific element from the delimited string — use `substring` + `indexOf` to extract the exact slice without materialising the whole array.
- The delimiter does not exist in the string — `split` returns a single-element array containing the whole string. This is valid but may not be what you want; check with `contains` first.
- You need regex-based splitting — `split` uses a literal delimiter only.
- The string is null — `split` will fail on null input; guard with `isEmpty` first.

## Comparison

| Function | Direction | Use when |
|----------|-----------|----------|
| `split` | string → array | Breaking a delimited string into parts |
| `join` | array → string | Assembling an array of parts into a delimited string |
| `substring` + `indexOf` | string → string | Extracting one specific piece without building an array |

## Common mistakes

- **Null input**: `split` fails on null source. Use `isEmpty` as a guard before calling `split` on optional fields.
- **Path resolution**: arguments resolve against the document root (dataContext). `@.field` inside a function refers to the ROOT, not a parent element.
- **Accessing split results immediately by index**: after `split` stores a result at `$.tags`, you can reference `$.tags[0]`, `$.tags[1]`, etc. in subsequent steps. You cannot nest `split` inside another function call and index into it in the same expression.
- **Multi-character delimiter**: `split` supports multi-character delimiters (e.g., `", "`), but be precise — a trailing space in the delimiter will not match a plain comma-separated string.
- **Empty segments**: if the source string starts or ends with the delimiter, or has consecutive delimiters, `split` produces empty-string elements in the array. Handle these with `replace` before splitting if empty segments are unwanted.
