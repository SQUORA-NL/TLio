# multiply

> Multiplies all numeric arguments together. Arrays are flattened, so a single array argument is the product of its elements.

## Syntax

```
=multiply(<a>, <b>, ...)
=multiply(<array>)
```

## Arguments

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1..n | number, numeric array, or path | yes (at least one) | Every resolved value is multiplied into the running product. Arrays are flattened recursively. |

## Returns

A numeric node. Whole results come out as an integer (`24`, not `24.0`); fractional results keep their fraction.

## Example

```json
{ "command": "put", "path": "$.premium",
  "value": "=round(=multiply($.basePremium,$.regionFactor,$.ageFactor),2)" }
```

Input: `{ "basePremium": 42.5, "regionFactor": 1.2, "ageFactor": 0.8 }`
Output: `{ "basePremium": 42.5, "regionFactor": 1.2, "ageFactor": 0.8, "premium": 40.8 }`

A single array argument is the product of its elements:

```json
{ "command": "put", "path": "$.result", "value": "=multiply($.factors)" }
```

Input: `{ "factors": [2, 3, 4] }`
Output: `{ "factors": [2, 3, 4], "result": 24 }`

## When to use

- **A chain of rating or scaling factors** — base premium × region × age × mileage × usage. This is the case `multiply` was added for; it replaces `=calculate(=concat(a,'*',b,'*',c))`.
- **Unit conversion** — quantity × price, amount × rate, months × monthly premium.
- **The product of a whole collection** — `=multiply($.discountFactors)` or `=multiply($.items[*].factor)`; the wildcard result is flattened into one product.
- **Any place `sum` would be right if the operator were `+`** — the argument shape is identical.

## When NOT to use

- **Do not keep using `calculate` to multiply a list of numbers.** `=calculate(=concat(a,'*',b))` builds a `DataTable`, formats every operand into a string and parses an expression, inheriting `DataTable.Compute`'s locale and precision rules — to do what `multiply` does directly. Keep `calculate` for a genuinely free-form expression string, one with mixed operators, parentheses, or an operator that is itself data.
- You need a **product with a rounding step folded in** — there is no rounded variant. Write `=round(=multiply(...),2)`; rounding is a separate decision.
- Any factor **may be null and should be skipped** — it will not be skipped. See Common mistakes.
- You need **repeated multiplication of one value by itself** — use `pow`.

## Comparison

| Function | Operator | Arity | Arrays | `[2,3,4]` gives |
|----------|----------|-------|--------|-----------------|
| `multiply` | `×` | variadic | flattened into the product | `24` |
| `sum` | `+` | variadic | flattened into the total | `9` |
| `divide` | `÷` | exactly 2 | each side summed first | n/a |
| `calculate` | any, from a string | 1 expression | no | n/a — needs a formula string |
| `pow` | `^` | exactly 2 | no (single scalar each) | n/a |

## Common mistakes

- **Found-but-null multiplies as 0, not as 1.** `=multiply($.a,$.missingFactor)` where `missingFactor` is present and `null` returns `0`, collapsing the whole chain — and one `null` element inside an array argument collapses that product too. This is deliberate: every function in the Math pack maps a found null to 0, and `multiply` does not get its own rule. A rate table with a hole should produce a visibly wrong zero, not a premium priced as if the factor were neutral. Guard the value first (`=fetch($.factor,1)`) if 1 is what you mean.
- **Path not found is a failure, not a zero.** A null that *exists* is 0; a path that resolves to nothing fails the function and aborts the script. The two are different states.
- **A wildcard path is one argument, not several.** `=multiply($.items[*].factor)` multiplies every matched node together — that is usually what you want, but it means adding a second matching element silently changes the result.
- **Non-numeric strings fail.** Numeric strings (`"2.5"`) are parsed with the invariant culture; `"2,5"` and `"abc"` fail the function.
- **Wrong path scope** — path arguments resolve against the document root, not the current node.
