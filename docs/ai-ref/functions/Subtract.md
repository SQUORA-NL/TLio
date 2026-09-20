# subtract

> Subtracts the second argument from the first. Both sides accept arrays, which are summed before the subtraction.

## Syntax

```
=subtract(<base>, <subtract>)
```

## Arguments

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | number, numeric array, or path | yes | The base value. All values it matches are summed first. |
| 2 | number, numeric array, or path | yes | The value to subtract. Also summed first. |

Exactly two arguments are used — the second checks only `Arguments.Count < 2`, so a third
argument is silently ignored rather than rejected.

## Returns

A numeric node equal to `base − subtract`. Whole results come out as an integer (`5`, not
`5.0`).

## Example

```json
{ "command": "put", "path": "$.margin", "value": "=subtract($.premium,$.cost)" }
```

Input: `{ "premium": 412.5, "cost": 398.2 }`
Output: `{ "premium": 412.5, "cost": 398.2, "margin": 14.3 }`

Arrays are summed before subtracting — the mirror of `divide`:

```json
{ "command": "put", "path": "$.result", "value": "=subtract($.nums,$.b)" }
```

Input: `{ "nums": [10, 5], "b": 3 }`
Output: `{ "nums": [10, 5], "b": 3, "result": 12 }`

## When to use

- **A difference between two amounts or dates-as-numbers** — renewal minus quoted premium,
  actual minus budgeted, claims paid minus premium collected.
- **Undoing an addition step** in a rating chain, alongside `sum`.
- **Feeding `sign`** to classify the direction of a change: `=sign(=subtract($.a,$.b))`.

## When NOT to use

- **Do not keep using `calculate` to subtract numbers.** `=calculate(=concat(a,'-',b))` welds
  the operator into a string and pays for a `DataTable` parse to do what `subtract` does
  directly. Keep `calculate` for a genuinely free-form expression string.
- You want **more than two operands** — `subtract` only ever uses the first two arguments;
  chain calls (`=subtract(=subtract($.a,$.b),$.c)`) or use `sum` with negated values instead.
- You want the **absolute difference**, sign discarded — wrap it: `=abs(=subtract($.a,$.b))`.

## Comparison

| Function | Operation | Arity | Arrays | Extra arguments |
|----------|-----------|-------|--------|------------------|
| `subtract` | `a − b` | uses first 2 | each side summed first | ignored, not rejected |
| `sum` | `a + b + …` | variadic | flattened into the total | all used |
| `divide` | `a ÷ b` | exactly 2 | each side summed first | n/a (fails on 3rd? no — see Divide.md) |
| `modulo` | remainder of `a ÷ b` | exactly 2 | no (single scalar each) | n/a |

## Common mistakes

- **A `null` operand is a zero, not a failure.** Found-but-null is 0 throughout the Math
  pack, so `=subtract($.a,$.nullField)` is just `a`. A path that resolves to nothing fails
  the function instead — the two nulls are different states.
- **An array argument is summed, not subtracted element-wise.** `=subtract($.nums,2)` with
  `nums: [10,5]` is `15 - 2 = 13`, not `[8, 3]`.
- **A third argument does nothing.** `=subtract($.a,$.b,$.c)` silently drops `$.c` — it is
  never read. Chain another call if a third term is needed.
- **Argument order.** The first argument is the base value; `=subtract($.total,$.discount)`,
  not the other way round.
- **Wrong path scope** — path arguments resolve against the document root, not the current
  node.
