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
| 2 | string literal | yes | The value to match (single-quoted). See [SumIf.md — criteria syntax](SumIf.md) for operators and wildcards. |
| 3 | array path | no | Array to average, parallel to `range`. Defaults to `range` itself when omitted. |

## Returns

A numeric node — the mean of the matching elements. **`0` — not a failure — when nothing
matches**, unlike `minifs`/`maxifs`.

## Example

Two-argument form (average the matching elements of the range itself):

```json
{ "command": "set", "path": "$.avg_high_scores", "value": "=averageif($.scores,'>=80')" }
```

Input: `{ "scores": [95, 60, 88, 40] }` → `"avg_high_scores": 91.5` — mean of `95` and `88`.

Three-argument form (criteria on one array, average a parallel array):

```json
{
  "command": "set", "path": "$.avg_paid_amount",
  "value": "=averageif($.status,'paid',$.amounts)"
}
```

Input: `{ "status": ["paid","pending","paid"], "amounts": [100, 200, 150] }`
Output: `"avg_paid_amount": 125` — mean of `100` and `150`.

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
