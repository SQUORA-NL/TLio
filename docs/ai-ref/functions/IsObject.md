# isObject

> Returns true when a value is an object (a node with named properties).

## Syntax

```
=isObject(<value>)
```

## Arguments

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | any or path | yes | The value to test. |

## Returns

A boolean node. A missing path is `false`.

## Verified example

```json
{ "command": "add", "path": "$.out", "value": "=isObject($.address)" }
```

Input: `{ "address": { "city": "Utrecht" }, "tags": ["a", "b"] }` → Output: `{ ..., "out": true }`

Verified by `PredicateFunctionTests.TypeChecks("=isObject($.address)", true)` in
`TLio.Functions.Tests/FunctionsTests/LogicTests/PredicateFunctionTests.cs:128`. The same method
proves an array is not an object (`=isObject($.tags)` → `false`, line 129).

As an `ifElse` condition:

```json
{ "command": "ifElse",
  "condition": "=isObject($.address)",
  "ifScript":   [{ "command": "copy", "from": "$.address.city", "path": "$.city" }],
  "elseScript": [{ "command": "add",  "path": "$.city", "value": "=fetch($.address)" }] }
```

## When to use

- Handling a field that may be either a nested object or a flat scalar (a very common shape difference between source systems).
- Guarding a `copy`/`merge` that assumes a nested structure.

## When NOT to use

- Checking for a specific property — use `exists($.address.city)`.

## Common mistakes

- **Arrays are not objects**: `=isObject($.items)` is false for a list. Use `isArray`.
- **An empty object is still an object**: `{}` gives true.
