# if

> Returns one of two values depending on a condition — the conditional *value*, not the conditional *command*.

## Syntax

```
=if(<condition>, <whenTrue>, <whenFalse>)
```

## Arguments

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | boolean, path or predicate call | yes | The condition. Truthiness follows the same rule as `ifElse` and `decisionTable`: `true`, or the text `"true"` (case-insensitive). Anything else — including numbers, `null`, and a path matching nothing — is false. |
| 2 | any value, path or function call | yes | Returned when the condition is true. Evaluated **only** when the condition is true. |
| 3 | any value, path or function call | yes | Returned when the condition is false. Evaluated **only** when the condition is false. |

All three arguments are required. There is no two-argument form that returns `null` when false:
in this library a null slot is a container a later command can write into (see
`docs/behaviour-decisions.md`), so handing one out implicitly would be the wrong default. Write
the false branch.

## Returns

The resolved value of the chosen branch. A branch that is a path is fetched; a branch that is a
literal is returned as-is; a branch that is a function call is executed. If the **chosen** branch
is a path that matches nothing, the function fails (and the script aborts) — the branch not
chosen is never touched.

## Example

```json
{ "command": "put", "path": "$.calc.excess",
  "value": "=if(=lessThan($.calc.driverAge,24),=sum($.request.cover.voluntaryExcess,300),=fetch($.request.cover.voluntaryExcess))" }
```

Input: `{ "calc": { "driverAge": 22, "excess": 0 }, "request": { "cover": { "voluntaryExcess": 300 } } }`
Output: `{ "calc": { "driverAge": 22, "excess": 600 }, "request": { "cover": { "voluntaryExcess": 300 } } }`

Lazy evaluation makes a guarded fetch safe:

```json
{ "command": "add", "path": "$.nickname", "value": "=if(=exists($.nick),=fetch($.nick),'-')" }
```

Input: `{ "name": "Sanne" }`
Output: `{ "name": "Sanne", "nickname": "-" }`

`fetch($.nick)` would fail and abort the script if it ran, but it never runs — the condition
selected the other branch.

## When to use

- One field, produced two ways: a rate, a label, a flag, a default. This is the `ifElse`-as-ternary case.
- Guarding an expression that fails on absent data: `=if(=exists(p),=fetch(p),'fallback')`.
- Inside another expression, where a command cannot go: `=round(=multiply($.base,=if($.vip,0.9,1.0)),2)`.
- Choosing between two computed values without writing an intermediate field to the document first.

## When NOT to use

- **Structural branching — use the `ifElse` command.** `if` produces a *value*. When the two
  branches differ in shape rather than in content — adding a clause object, removing a node,
  renaming something, running several commands, writing to two different paths — that is
  `ifElse`, and `if` does not replace it.
- **Three or more outcomes — use the `decisionTable` command.** Nesting `if` inside `if` inside
  `if` to walk a band table is exactly what `decisionTable` exists for; it has priority ordering
  and a default row and reads as a table.
- **Only one fallback for one missing path — use `fetch(path, default)`**, or `coalesce` for
  several candidate sources. `=if(=exists($.a),=fetch($.a),'-')` and `=fetch($.a,'-')` mean the
  same thing; the second is shorter.
- **Testing a numeric range — use `between`.** `=if(=and(=greaterOrEqual(v,lo),=lessOrEqual(v,hi)),…)` is `=if(=between(v,lo,hi),…)`.

## Comparison

| | Kind | Produces | Branches | Use when |
|---|---|---|---|---|
| `if` function | value | one node | 2 | One field, two possible values |
| `ifElse` command | command | document changes | 2 (each a whole script) | The branches differ in *structure*, or run several commands |
| `decisionTable` command | command | document changes | many, with priority + default | Three or more outcomes keyed on one or more inputs |
| `coalesce` function | value | one node | n candidates | "First of these that has a value", not "this or that" |

The choice between `if` and `ifElse` is **value versus structure**. If the answer to "what
changes?" is "the content of one field", it is `if`. If it is "what the document looks like", it
is `ifElse`.

## Common mistakes

- **Expecting a number to be truthy.** `=if($.count,'some','none')` returns `'none'` even when
  `count` is `7`. Only `true` and the text `"true"` are truthy — the same rule `ifElse` uses.
  Write `=if(=greaterThan($.count,0),'some','none')`.
- **Assuming both branches are safe to write.** Only the *unchosen* branch is protected. If the
  branch that is taken is a path matching nothing, or a function that fails, the script aborts.
  Laziness guards the other branch, not this one.
- **Reaching for a two-argument form.** `=if(c, v)` fails with a warning; it does not return
  null. Supply the third argument.
- **Using it for structure.** `=if(c, $.wholeObject, $.otherObject)` works and returns an object,
  but if what you actually want is "add this clause when covered", the `ifElse` command says so
  in the script where a reader will look for it.
- **Extra arguments are ignored.** `=if(c,a,b,c2)` does not error; arguments past the third are
  never read. Check the parentheses if a branch seems to be missing.
- **Path args resolve against the document root.** Inside the call, use `$.field`.
