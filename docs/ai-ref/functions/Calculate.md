# calculate

> Evaluates a free-form arithmetic expression string at run time, via `System.Data.DataTable.Compute`.

## Syntax

```
=calculate(<expression>)
```

## Arguments

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | string or path | yes | A single expression string, e.g. `'2 + 3'`, `'10 * (4 - 1)'`, `'7 % 3'`. Must resolve to a string node. |

Supported operators: `+`, `-`, `*`, `/`, `%` and parentheses — whatever
`DataTable.Compute` accepts.

## Returns

A numeric node with the computed result (invariant-culture parsed).

## Example

```json
{ "command": "put", "path": "$.result", "value": "=calculate('(2 + 3) * 4')" }
```

Output: `{ "result": 20 }`

Building the expression dynamically from document values:

```json
{ "command": "put", "path": "$.total", "value": "=calculate(=concat($.a,'+',$.b))" }
```

Input: `{ "a": 5, "b": 3 }` → `{ "a": 5, "b": 3, "total": 8 }`

## When to use

- The expression itself is **genuinely dynamic** — the operator, or the number of operands,
  is determined by data rather than by the script author (e.g. a formula string stored in a
  reference table).
- A one-off arithmetic mix of operators and parentheses that would otherwise need several
  nested `sum` / `subtract` / `multiply` / `divide` calls.

## When NOT to use

- **A fixed chain of `+`, `-`, `*` or `÷` known at script-writing time** — use `sum`,
  `subtract`, `multiply`, `divide` directly. `calculate(=concat(a,'*',b))` builds a
  `DataTable`, formats every operand into a string, and parses an expression to do what
  `multiply` does directly — see [Multiply.md](Multiply.md) and [Divide.md](Divide.md).
- **A comma-decimal source.** `DataTable.Compute` rejects `'2,5+3,7'` outright — it does not
  quietly misparse it the way string-to-number coercion elsewhere in TLio does. See
  [behaviour-decisions.md §A1](../../behaviour-decisions.md#a1-35-becomes-35-in-every-math-function).
- **Untrusted expression text.** The expression is handed to `DataTable.Compute` verbatim;
  treat it the same as any other run-time-evaluated formula, not as safe user input.

## Comparison

| Function | Operators | Operand count | Expression source |
|----------|-----------|----------------|--------------------|
| `calculate` | any of `+ - * / %`, mixed, parenthesised | one expression string | data or literal |
| `sum` | `+` only | variadic | function arguments |
| `subtract` | `-` only | first 2 arguments | function arguments |
| `multiply` | `*` only | variadic | function arguments |
| `divide` | `/` only | exactly 2 | function arguments |
| `modulo` | `%` only | exactly 2 | function arguments |

## Common mistakes

- **A malformed or unparsable expression fails, it does not return an error value.** Division
  by zero, mismatched parentheses, or a non-numeric operand all raise the underlying
  `DataTable.Compute` exception, which `calculate` turns into a failed function — a failed
  function aborts the script.
- **The argument must resolve to a string, not a number.** `=calculate(5)` is not a valid
  expression argument in the way `=sum(5)` would be — pass the formula as a string:
  `=calculate('5+2')`.
- **Locale is invariant, not comma-decimal.** `'2,5'` inside the expression is not the number
  2.5; normalise commas to periods before building the expression string if the source data
  uses comma decimals.
- **Wrong path scope** — a path argument resolves against the document root, not the current
  node.
