# isBoolean

> Returns true when a value is a boolean in the document.

## Syntax

```
=isBoolean(<value>)
```

## Arguments

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | any or path | yes | The value to test. |

## Returns

A boolean node. A missing path is `false`.

## Verified example

```json
{ "command": "add", "path": "$.out", "value": "=isBoolean($.active)" }
```

Input: `{ "active": true, "activeText": "true" }` → Output: `{ ..., "out": true }`

Verified by `PredicateFunctionTests.TypeChecks("=isBoolean($.active)", true)` in
`TLio.Functions.Tests/FunctionsTests/LogicTests/PredicateFunctionTests.cs:123`. The same method
proves the text `"true"` is not a boolean (`=isBoolean($.activeText)` → `false`, line 124) —
even though `and`/`or`/`not`/`ifElse` treat that same text as truthy.

As an `ifElse` condition:

```json
{ "command": "ifElse",
  "condition": "=isBoolean($.active)",
  "ifScript":   [{ "command": "copy", "from": "$.active", "path": "$.status.enabled" }],
  "elseScript": [{ "command": "add",  "path": "$.errors", "value": ["active must be true or false"] }] }
```

## When to use

- Validating flags that may arrive as `"true"` text instead of `true`.
- Deciding whether a value can be used directly as an `ifElse` condition.

## When NOT to use

- Testing whether a condition holds — that is what the value itself, or a predicate, is for.

## Common mistakes

- **Text booleans**: in JSON, `"true"` is a string, so `isBoolean` is false — even though `ifElse` and `and`/`or`/`not` do treat that text as truthy.
