# isEmpty

> Returns true when a present value is null, an empty string, or an empty array. A missing path fails.

## Syntax

```
=isEmpty(<source>)
```

## Arguments

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | string, array or path | yes | The value to check. |

## Returns

A boolean node. `true` when the value is `null`, `""`, or `[]`. `false` for everything else,
**including a whitespace-only string** — `isEmpty("   ")` is `false`.

**A path matching nothing is not `true` — it fails and aborts the script**, exactly like a
`fetch` with no default. This is `isEmpty`'s one exception from the predicate rule the rest of
the Logic pack follows (`isNull`, `exists`, `in`, etc. all treat a missing path as an answer, not
a failure). Guard with `exists` first when the field may not be present at all:
`=and(exists($.field),isEmpty($.field))` distinguishes "absent" from "present and blank".

The check is `string.IsNullOrEmpty`, not `string.IsNullOrWhiteSpace`. This is a real, deliberate
difference from what the name suggests: only `""` counts as an empty string, not `"   "`. If
whitespace-only content should also count as empty, trim first:
`=isEmpty(=trim($.field))`.

## Verified example

```json
{ "command": "put", "path": "$.result", "value": "=isempty($.empty)" }
```

Input: `{ "str": "hello", "empty": "", "nul": null, "arr": [], "arrFull": [1] }`
Output: `{ ..., "result": true }`

Verified by: `TLio.Functions.Tests/Fixtures/Text/isempty/02-empty-string.json`. The same fixture
group also covers a non-empty string (`01-non-empty-string.json`, `result: false`), `null`
(`03-null.json`, `result: true`), an empty array (`04-empty-array.json`, `result: true`), and a
non-empty array (`05-non-empty-array.json`, `result: false`). At the unit level,
`IsEmptyTests.IsEmpty_EmptyString_ReturnsTrue` and the sibling `IsEmpty_*` tests in
`TLio.Functions.Tests/FunctionsTests/TextTests/IsEmptyTests.cs` assert the same cases directly
against the source, which uses `string.IsNullOrEmpty` — confirmed by reading
`TLio.Extensions.Text/IsEmpty.cs`. None of the existing tests exercise a whitespace-only string;
that behaviour follows directly from `string.IsNullOrEmpty` never treating `"   "` as empty.

## When to use

- You need a blank check for a string that is present in the document (`null` or `""`), and you
  are sure the path itself resolves — guard with `exists` first if it might not.
- You need the same "is this absent?" check to also cover an empty array — `isEmpty([])` is `true`, unlike `length([])` which needs a zero comparison.
- You want to set a flag that guards later steps from operating on absent or blank values.
- Replacing a manual multi-condition check: `isEmpty` is simpler and safer than checking null and length separately.

## When NOT to use

- **The field may be whitespace-only and that should count as empty** — `isEmpty` does not treat
  `"   "` as empty. Trim first: `=isEmpty(=trim($.field))`.
- You only need to check a specific prefix or suffix — use `startsWith` / `endsWith` (but guard them with `isEmpty` first if the field may be null).
- You need to check for a specific substring — use `contains` (but guard with `isEmpty` first if needed).
- You need the length of the string or array — use `length` (returns 0 for null/empty without throwing, and the element count for an array).

## Comparison

| Function | Missing path | `null` value | Whitespace-only counts as empty | Empty array | Returns |
|----------|--------------|--------------|----------------------------------|-------------|---------|
| `isEmpty` | fails the script | `true` | no — use `isEmpty(trim(...))` | `true` | boolean |
| `contains` | fails | treated as `""` (rarely matches) | n/a | n/a | boolean |
| `startsWith` | fails | treated as `""` | n/a | n/a | boolean |
| `endsWith` | fails | treated as `""` | n/a | n/a | boolean |
| `length` | fails | `0` | no (counts the spaces) | element count (not a boolean) | integer |

## Common mistakes

- **Assuming whitespace-only counts as empty.** It does not — `isEmpty("   ")` is `false`,
  because the source is `string.IsNullOrEmpty`, not `IsNullOrWhiteSpace`. Use
  `=isEmpty(=trim($.field))` when whitespace-only content should be treated as blank.
- **Confusing isEmpty with a length-zero check**: for strings the two agree except on
  whitespace-only content, where they disagree — `isEmpty` says `false`, `length(x) == 0` also
  says false (the spaces count), so neither treats `"   "` as empty on its own; `trim` first if
  that is what you want.
- **Using isEmpty to check numeric fields**: `isEmpty` operates on the string representation. For numeric zero-checks, compare the value directly.
- **Path resolution**: argument resolves against the document root (dataContext). `@.field` inside a function refers to the ROOT, not a parent element.
- **Wildcard paths**: `$.items[*].name` as argument grabs all values as a flat list. Use indexed paths for per-element operations.
