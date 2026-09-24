# averageif

> Averages the elements of a range (or a separate `average_range`) where a single criteria matches.

## Syntax

```
=averageif(<range>, <criteria>)
=averageif(<range>, <criteria>, <average_range>)
```

## Arguments

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | array path | yes | Path to the array evaluated against `criteria`. |
| 2 | string literal or path | yes | The criteria to match — single-quoted literal or a path to a string node. See [SumIf.md — criteria syntax](SumIf.md) for operators and wildcards. |
| 3 | array path | no | Array to average, parallel to `range`. Defaults to `range` itself when omitted. |

## Returns

A numeric node — the mean of the matching elements. **`0` — not a failure — when nothing
matches**, unlike `minifs`/`maxifs`.

## Verified example

Two-argument form (average the matching elements of the range itself):

```json
{ "command": "put", "path": "$.result", "value": "=averageif($.nums, $.crit_gt3)" }
```

Input: `{ "nums": [1, 2, 3, 4, 5], "values": [10, 20, 30, 40, 50], "cat": ["A", "B", "A", "C", "A"], "crit_gt3": ">3", "crit_A": "A", "crit_Z": "Z" }`
Output: `..., "result": 4.5` — mean of `4` and `5`.

Verified by: `TLio.Functions.Tests/Fixtures/Math/averageif/01-numeric-criteria.json`.

Three-argument form (criteria on one array, average a parallel array):

```json
{ "command": "put", "path": "$.result", "value": "=averageif($.cat, $.crit_A, $.values)" }
```

Same input → `..., "result": 30` — mean of `10`, `30`, `50` (the three `"A"` positions in
`$.cat`, read from the parallel `$.values`).

Verified by: `TLio.Functions.Tests/Fixtures/Math/averageif/02-with-average-range.json`. The
sibling `03-no-match.json` runs `=averageif($.cat, $.crit_Z)` (nothing is `"Z"`) → `0`, not a
failure — confirming the no-match contract above.

## When to use

- A **conditional mean** where `sumif` plus a manual count would otherwise be needed.
- Reporting an **average of matching records** — average claim size for a status, average
  premium for a segment.

## When NOT to use

- You need the **total**, not the mean — use `sumif`.
- You need the **count** of matches — use `countif`.
- You need **more than one condition** — use `averageifs`.
- The result should **fail** rather than read `0` when nothing matches — `averageif` never
  fails on a no-match; guard with `countif` first if that distinction matters.

## Performance

`averageif` walks `min(range.Count, averageRange.Count)` once, evaluating the criteria per
element — O(n). Like the rest of the `*if`/`*ifs` family, the criteria string is parsed fresh
on every call rather than compiled once, so the per-call overhead is dominated by the array
scan for anything but a tiny range.

## Comparison

| Function | Aggregate | Conditions | Nothing matches |
|----------|-----------|-----------|------------------|
| `averageif` | mean | exactly 1 | `0` |
| `averageifs` | mean | 1 or more (AND) | `0` |
| `sumif` | total | exactly 1 | `0` |
| `avg` | mean | none (unconditional) | fails on empty input |

## Common mistakes

- **`0` on no match looks like a real zero average.** A genuine average of `0` and "nothing
  matched" are indistinguishable in the result; check with `countif` first if the difference
  matters.
- **Arrays must be parallel** when `average_range` is given — same length, same index
  alignment as `range`.
- **Using double quotes for criteria** — must be single-quoted.
- **Wrong path scope** — both array arguments resolve against the document root, not the
  current node.
