# divide

> Divides the first argument by the second. Both sides accept arrays, which are summed before the division.

## Syntax

```
=divide(<dividend>, <divisor>)
```

## Arguments

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | number, numeric array, or path | yes | The dividend. All values it matches are summed first. |
| 2 | number, numeric array, or path | yes | The divisor. Also summed first. Must not resolve to 0. |

## Returns

A numeric node equal to `dividend / divisor`. Whole results come out as an integer (`5`, not `5.0`).

## Verified example

```json
{ "command": "put", "path": "$.perMonth",
  "value": "=round(=divide(=multiply($.currentValue, $.cascoRate), 12), 2)" }
```

Input: `{ "currentValue": 18000, "cascoRate": 0.042 }`
Output: `{ "currentValue": 18000, "cascoRate": 0.042, "perMonth": 63 }`

Verified by: `TLio.Functions.Tests/Fixtures/Math/divide/04-monthly-instalment.json`

Arrays are summed before dividing — the mirror of `subtract`:

```json
{ "command": "put", "path": "$.result", "value": "=divide($.nums, $.b)" }
```

Input: `{ "nums": [1, 2, 3], "b": 2 }`
Output: `{ "nums": [1, 2, 3], "b": 2, "result": 3 }`

Verified by: `TLio.Functions.Tests/Fixtures/Math/divide/03-array-dividend-summed.json`

## When to use

- **Splitting an annual or term amount into instalments** — yearly premium ÷ 12, term total ÷ number of terms.
- **Rates and ratios** — claims ÷ policies, part ÷ whole, before multiplying by 100 for a percentage.
- **Undoing a scale factor** in a rating chain, alongside `multiply`.
- **An average over a collection where you already have the total** — although `avg` is usually the clearer call.

## When NOT to use

- **Do not keep using `calculate` to divide numbers.** `=calculate(=concat(a,'/',b))` welds the operator into a string and pays for a `DataTable` parse. Keep `calculate` for a genuinely free-form expression string — mixed operators, parentheses, or an operator that is itself data.
- The divisor **might be zero** — `divide` fails rather than emitting `Infinity`, and a failed function aborts the script. Guard the divisor, or branch with `ifElse`, before dividing.
- You want the **remainder** rather than the quotient — use `modulo`.
- You want the **whole-number quotient** — wrap it: `=floor(=divide($.a,$.b))`.
- You want the **arithmetic mean** of an array — `avg` already does the sum and the count.

## Comparison

| Function | Operation | Arity | Arrays | Zero second argument |
|----------|-----------|-------|--------|----------------------|
| `divide` | `a ÷ b` | exactly 2 | each side summed first | fails |
| `subtract` | `a − b` | exactly 2 | each side summed first | fine — `a − 0` is `a` |
| `modulo` | remainder of `a ÷ b` | exactly 2 | no (single scalar each) | fails |
| `multiply` | `a × b × …` | variadic | flattened into the product | fine |
| `avg` | mean of all values | variadic | flattened | n/a |

## Common mistakes

- **A zero divisor fails the whole script.** `divide` never returns `Infinity` or `NaN` — it logs an error and fails, and a failed function aborts execution (behaviour decision B2). If a zero divisor is a real possibility in your data, guard it before the call rather than cleaning up afterwards.
- **A `null` divisor is a zero divisor.** Found-but-null is 0 throughout the Math pack, so `=divide($.a,$.nullField)` fails for the same reason `=divide($.a,0)` does.
- **An array argument is summed, not divided element-wise.** `=divide($.nums,2)` with `nums: [1,2,3]` is `6 / 2 = 3`, not `[0.5, 1, 1.5]`. `divide` returns one number, never a collection.
- **Argument order.** The first argument is the dividend. `=divide($.total,$.count)`, not the other way round.
- **Path not found is a failure, not a zero** — for either argument.
- **Wrong path scope** — path arguments resolve against the document root, not the current node.
