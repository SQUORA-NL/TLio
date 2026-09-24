# matches

> Returns true when a value's text matches a regular expression.

## Syntax

```
=matches(<value>, <pattern>)
```

## Arguments

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | any or path | yes | The value to test; read as text. |
| 2 | string or path | yes | A .NET regular expression. |

## Returns

A boolean node. A missing value is `false`. An invalid pattern logs an error and produces no value.

## Verified example

```json
{ "command": "add", "path": "$.out", "value": "=matches($.email, '^[^@]+@[^@]+\\\\.[a-z]+$')" }
```

Input: `{ "email": "sanne@example.nl" }` → Output: `{ ..., "out": true }`

Verified by `PredicateFunctionTests.MembershipAndPattern` in
`TLio.Functions.Tests/FunctionsTests/LogicTests/PredicateFunctionTests.cs:142` — run end to end
through the real engine (the doubled backslashes there are the C# raw-string escaping of the
JSON escaping). The same method also proves it is unanchored by default
(`=matches($.name, '^S')` → `true` for `"Sanne"`, line 143) and that a missing value is `false`
rather than a failure (`=matches($.missing, '.*')` → `false`, line 145). An invalid pattern is
covered separately by `PredicateFunctionTests.InvalidRegexPattern_LogsErrorAndFails`, which
asserts an `Error`-level log entry and no output value.

As an `ifElse` condition:

```json
{ "command": "ifElse",
  "condition": "=matches($.emailAddress, '^[^@]+@[^@]+\\.[a-z]{2,}$')",
  "ifScript":   [{ "command": "add", "path": "$.emailValid", "value": true }],
  "elseScript": [{ "command": "add", "path": "$.emailValid", "value": false }] }
```

## When to use

- Format validation: postcodes, IBANs, e-mail addresses, product codes.
- Checks that prefix/suffix/substring functions cannot express.

## When NOT to use

- Simple prefix, suffix or substring tests — `startsWith`, `endsWith` and `contains` are clearer and cheaper.
- Extracting part of a value — `matches` only answers yes/no; use `substring` with `indexOf`, or `split`.

## Common mistakes

- **Unanchored by default**: `=matches($.code, 'AB')` is true for `"XABY"`. Anchor with `^` and `$` for a full match.
- **Escaping in JSON**: a backslash must be doubled in the JSON string — `'^\\d{4}$'` for four digits.
- **Matching a number**: the value is read as text first, so `=matches($.year, '^\\d{4}$')` works on the number 2026.
- **Runaway patterns**: matching is capped at one second; a catastrophically backtracking pattern logs an error rather than hanging.
