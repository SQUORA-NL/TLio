# calculate

> Evaluates a free-form arithmetic expression string at run time, via `System.Data.DataTable.Compute`.

## Syntax

```
=calculate(<expression>)
```

## Arguments

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | string or path | yes | A single expression string, e.g. `'2 + 3'`, `'10 * (4 - 1)'`, `'7 % 3'`. Resolved through `TryGetString`, so a numeric node (`=calculate($.n)` where `$.n` is `42`) also works; an object or array node does not and fails the function. Only the first argument is read — extras are ignored. |

Supported operators: `+`, `-`, `*`, `/`, `%` and parentheses — whatever
`DataTable.Compute` accepts.

## Returns

A numeric node with the computed result (invariant-culture parsed).

## Verified example

```json
{ "command": "put", "path": "$.result", "value": "=calculate($.complex)" }
```

Input: `{ "add": "2 + 3", "complex": "10 * (4 - 1)", "modulo": "7 % 3" }`
Output: `{ "add": "2 + 3", "complex": "10 * (4 - 1)", "modulo": "7 % 3", "result": 30 }`

`10 * (4 - 1)` demonstrates both operator precedence and parentheses in one expression:
the parenthesised subtraction (`4 - 1 = 3`) evaluates first, then the multiplication
(`10 * 3 = 30`) — the same left-to-right, parens-first rule as ordinary arithmetic.

Verified by: `TLio.Functions.Tests/Fixtures/Math/calculate/02-complex-expression.json`
(siblings `01-addition.json` and `03-modulo-expression.json` in the same directory cover
`+` and `%` against the same input document).

Building the expression dynamically from document values, via composition with `concat`:

```json
{ "command": "add", "path": "$.result", "value": "=calculate(=concat($.a,'+',$.b))" }
```

Input: `{ "a": "2", "b": "3" }` → `$.result` becomes `5`.

Verified by: `TLio.Functions.Tests/FunctionsTests/MathTests/CalculateTests.cs` →
`ExpressionCanBeBuiltByAnInnerFunction`.

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

## Performance

The expression is **not parsed once and cached** — `Calculate<TNode>.Execute` constructs a
fresh `System.Data.DataTable` and calls `dt.Compute(expression, null)` on *every* invocation
(`TLio.Extensions.Math/Calculate.cs`), so the full parse-and-evaluate cost of `DataTable.Compute`
is paid per call, including inside a loop such as `setProperties` writing through a wildcard
selection. If the same expression *shape* runs many times (e.g. once per array element), prefer
a fixed-operator function (`sum`, `subtract`, `multiply`, `divide`, `modulo`) where one exists —
those avoid the `DataTable` round trip entirely. Reserve `calculate` for expressions whose
operators or operand count genuinely vary at run time.

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

- **A malformed or unparsable expression fails; it does not return an error value.** Mismatched
  parentheses, a non-numeric operand, or a stray operator all raise the underlying
  `DataTable.Compute` exception, which `calculate` turns into a failed function — a failed
  function aborts the script. **Division by zero is the exception to this**: `calculate('5/0')`
  *succeeds* — `DataTable.Compute` promotes to floating point and yields `double.Infinity`,
  which Newtonsoft serialises as the JSON **string** `"Infinity"` (`0/0` → the string `"NaN"`),
  not a number. Verified by `CalculateTests.DivisionByZero_ProducesANonFiniteNumber` and
  `.ZeroDividedByZero_ProducesNaN`. A caller that reads `$.result` expecting a number can be
  handed a string instead — guard the denominator rather than relying on the function to fail.
- **No `{{$.path}}` interpolation inside the expression string.** Unlike JLio, TLio does not
  substitute `{{$.path}}` tokens inside the expression — it reaches `DataTable.Compute` verbatim
  and fails on the literal `{`. Build the expression with `=concat(...)` instead (see the second
  verified example above), or resolve the whole expression from one path with `=calculate($.path)`.
- **Locale is invariant, not comma-decimal.** `'2,5'` inside the expression is not the number
  2.5; it reads as an argument separator and is a syntax error. Normalise commas to periods
  before building the expression string if the source data uses comma decimals.
- **Wrong path scope** — a path argument resolves against the document root, not the current
  node.
