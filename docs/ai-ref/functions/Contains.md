# contains

> Returns true if a string contains the specified substring. **Case-insensitive.**

## Syntax

```
=contains(<source>, <substring>)
```

## Arguments

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | string or path | yes | The string to search in. |
| 2 | string or path | yes | The substring to look for. |

## Returns

A boolean node. The comparison is `StringComparison.OrdinalIgnoreCase`, so `contains` matches
regardless of case — there is no way to opt into a case-sensitive check.

## Verified example

```json
{ "command": "put", "path": "$.result", "value": "=contains($.str, $.sub)" }
```

Input: `{ "str": "Hello World", "sub": "lo Wo", "no": "XYZ" }`
Output: `{ "str": "Hello World", "sub": "lo Wo", "no": "XYZ", "result": true }`

Verified by: `TLio.Functions.Tests/Fixtures/Text/contains/01-match.json` (and
`02-no-match.json`, `=contains($.str, $.no)` → `false`).

Case-insensitivity itself is covered by the inline test
`PredicateTests.Contains_CaseInsensitive` (`"Hello World"` contains `"hello"` → `true`).

## When to use

- You need to know whether a substring appears anywhere in a string (not just at the start or end).
- You want to store a boolean flag based on whether a field contains a given token, regardless of case.
- You are checking for presence before performing a conditional transformation downstream.

## When NOT to use

- You only need to check the start of the string — use `startsWith` instead (clearer intent).
- You only need to check the end of the string — use `endsWith` instead.
- You need to know the position of the substring — use `indexOf` instead.
- The input path may be **missing entirely** — that's the actual failure case, not null. A path that resolves to a `null` value is treated as `""` (see Common mistakes below); use `isEmpty` first if you need to distinguish "absent" from "empty" from "blank".
- You need a **case-sensitive** check — `contains` cannot do this; there is no built-in flag and no
  workaround via a value function (functions cannot be nested as the first argument to another
  function call in this notation). You would need a different mechanism entirely (e.g. `regexExtract`
  with an anchored, case-sensitive pattern check via `matches`).

## Comparison

| Function | Returns | Case | Use when |
|----------|---------|------|----------|
| `contains` | boolean | insensitive | Substring appears anywhere |
| `startsWith` | boolean | insensitive | String begins with prefix |
| `endsWith` | boolean | insensitive | String ends with suffix |
| `indexOf` | integer (-1 if missing) | insensitive | Position of substring matters |
| `isEmpty` | boolean | n/a | Need null-safe blank check |

## Common mistakes

- **Assuming it's case-sensitive**: `contains($.email,'Gmail')` DOES match `"gmail.com"` — the
  comparison is `OrdinalIgnoreCase`. Do not add a redundant `toLower` normalisation step expecting
  otherwise, and do not rely on `contains` to distinguish `"ABC"` from `"abc"`.
- **Null vs. missing**: a path that resolves to a `null` node does NOT fail — it's treated as `""` (so `=contains($.nullField, 'x')` → `false`, and `=contains($.nullField, '')` → `true`). Only a path that resolves to *nothing* (the field doesn't exist at all) fails. This is shared behavior across all `TLio.Extensions.Text` functions (`TextFunctionBase.TryGetStringArg`), not specific to `contains`. Guard with `isEmpty` if the field may be structurally absent.
- **Path resolution**: arguments resolve against the document root (dataContext), not the current node. `@.field` inside a function refers to the ROOT, not a parent element.
- **Using as a script-level condition**: `contains` produces a boolean value node. It is set as a VALUE in a `set`/`add` command — it is not a filter or branching condition in the script itself.
- **Wildcard paths inside functions**: `$.items[*].name` passed to `contains` grabs all values as a flat list; the function will not iterate per-element. Use indexed paths like `$.items[0].name` for element-level operations.
