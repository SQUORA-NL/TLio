# isNumber

> Returns true when a value is numeric in the document.

## Syntax

```
=isNumber(<value>)
```

## Arguments

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | any or path | yes | The value to test. |

## Returns

A boolean node. A missing path is `false`.

## Formats

In JSON, `37` is a number and `"37"` is not. In XML and YAML — which have no type system —
a scalar that looks numeric reads as a number.

## Verified example

```json
{ "command": "add", "path": "$.out", "value": "=isNumber($.age)" }
```

Input: `{ "age": 37, "ageText": "37" }` → Output: `{ ..., "out": true }`

Verified by `PredicateFunctionTests.TypeChecks("=isNumber($.age)", true)` in
`TLio.Functions.Tests/FunctionsTests/LogicTests/PredicateFunctionTests.cs:120`. The same method
proves numeric-looking *text* is not a number (`=isNumber($.ageText)` → `false`, line 122) and a
missing path is not a number either (`=isNumber($.missing)` → `false`, line 131).

As an `ifElse` condition:

```json
{ "command": "ifElse",
  "condition": "=isNumber($.premium)",
  "ifScript":   [{ "command": "add", "path": "$.premiumText", "value": "=toFixed($.premium, 2)" }],
  "elseScript": [{ "command": "add", "path": "$.errors", "value": ["premium is not numeric"] }] }
```

## When to use

- Validating that a field is safe to feed into Math-pack functions.
- Branching between "already a number" and "text that must be parsed".

## When NOT to use

- Testing whether text *can* be parsed as a number — `isNumber` reports the JSON type, not parseability. Use `matches($.value, '^-?\\d+(\\.\\d+)?$')` for that.

## Common mistakes

- **Assuming "42" is a number**: in JSON it is a string. This is deliberate — see [Notation Reference](../notation-reference.md), "Strings stay strings".
- **Booleans**: `true` is not a number; use `isBoolean`.
