# isString

> Returns true when a value is textual in the document.

## Syntax

```
=isString(<value>)
```

## Arguments

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | any or path | yes | The value to test. |

## Returns

A boolean node. A missing path is `false` — it has no type at all.

## Formats

JSON carries its own types, so `"37"` is a string and `37` is not. XML and YAML scalars have
no type system; there a value is reported by its apparent type, so `37` reads as a number.

## Verified example

```json
{ "command": "add", "path": "$.out", "value": "=isString($.name)" }
```

Input: `{ "name": "Sanne", "ageText": "37", "age": 37 }` → Output: `{ ..., "out": true }`

Verified by `PredicateFunctionTests.TypeChecks("=isString($.name)", true)` in
`TLio.Functions.Tests/FunctionsTests/LogicTests/PredicateFunctionTests.cs:117`. The same method
proves the JSON *type* wins over appearance — numeric text is still a string
(`=isString($.ageText)` → `true`, line 118) and an actual number is not
(`=isString($.age)` → `false`, line 119) — and that a missing path has no type at all
(`=isString($.missing)` → `false`, line 130).

As an `ifElse` condition:

```json
{ "command": "ifElse",
  "condition": "=isString($.amount)",
  "ifScript": [{ "command": "set", "path": "$.amount", "value": "=parse($.amount)" }] }
```

## When to use

- Detecting fields that arrived as text and need conversion before arithmetic.
- Validating an incoming document's shape before transforming it.

## When NOT to use

- Checking for blank — use `isEmpty`.
- Checking presence — use `exists`.

## Common mistakes

- **Expecting numeric text to be a number**: in JSON, `"37"` is `isString` true and `isNumber` false. That is the document's type, not the value's appearance.
- **Relying on it for XML/YAML**: those formats have no string/number distinction; the answer is based on appearance.
