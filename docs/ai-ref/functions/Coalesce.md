# coalesce

> Returns the first argument that resolves to a value that is neither null nor an empty string.

## Syntax

```
=coalesce(<a>, <b>, ...)
```

## Arguments

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1..n | any value, path or function call | at least one | Candidates, tried left to right. The first one that resolves to a node that is not null and not `""` wins. |

A candidate whose path matches nothing is **skipped**, not a failure — that is the whole point.
Evaluation stops at the first winner, so later candidates are never evaluated.

## Returns

The first qualifying node, whole — including objects and arrays.

**If nothing qualifies the function fails, and a failed function aborts the script.** That is
deliberate: "none of these sources had a value" is a mapping error, not an empty answer. **End
the argument list with a literal default** whenever the absent case is legitimate.

## Example

```json
{ "command": "add", "path": "$.email",
  "value": "=coalesce($.party.email,$.contact.email,$.broker.email,'unknown')" }
```

Input: `{ "party": { "email": null }, "contact": { "email": "" }, "broker": { "email": "sanne@example.nl" } }`
Output: `{ ..., "email": "sanne@example.nl" }`

Missing paths are skipped just like nulls:

```json
{ "command": "add", "path": "$.email", "value": "=coalesce($.party.email,$.contact.email,'unknown')" }
```

Input: `{ "party": { "role": "policyholder" } }`
Output: `{ "party": { "role": "policyholder" }, "email": "unknown" }`

## When to use

- **Several candidate sources for one field.** This is the SIVI/AFD mapping case: the same field
  lives under different party roles depending on the message, and any of three paths may carry it.
- Normalising an optional field that arrives as `null` from one system and `""` from another —
  both are skipped by the same expression.
- Replacing nested `fetch` defaults: `=fetch($.a,=fetch($.b,=fetch($.c,'-')))` is `=coalesce($.a,$.b,$.c,'-')`.
- Picking the first available address / phone / reference object without caring which one it was.

## When NOT to use

- **One path, one fallback — use `fetch(path, default)`.** It is shorter and says exactly that.
  Note the difference in what they skip: `fetch`'s default only fires when the path matches
  *nothing*; `coalesce` also skips a present-but-null and a present-but-empty-string value.
- **You want to *ask* whether something is empty — use `isEmpty`.** `coalesce` chooses a value;
  `isEmpty` returns a boolean for an `ifElse` or `decisionTable` condition.
- **Empty arrays and empty objects should count as absent.** They do **not** — `coalesce` returns
  `[]` and `{}` as values. Use `isEmpty` with `if` when an empty collection should fall through.
- **Two mutually exclusive values chosen by a condition — use `if`.** `coalesce` answers "first
  of these that exists", not "this or that depending on a rule".

## Comparison

| | Candidates | Skips | Absent case |
|---|---|---|---|
| `coalesce(a, b, …)` | many | missing path, `null`, `""` | Fails unless you end with a literal default |
| `fetch(path, default)` | one + default | missing path only (a present `null` is returned as `null`) | Returns the default |
| `isEmpty(value)` | one | — | Returns `true`; also `true` for `[]` |
| `if(cond, a, b)` | two, chosen by a rule | — | Fails if the chosen branch does not resolve |

## Common mistakes

- **Forgetting the literal default.** `=coalesce($.a,$.b)` with both absent does not produce
  `null` — it fails and aborts the whole script. If "no value" is a legal outcome, write
  `=coalesce($.a,$.b,'')` or `=coalesce($.a,$.b,'unknown')`.
- **Expecting `0` or `false` to be skipped.** They are values and they win. `=coalesce($.voluntaryExcess,300)`
  returns `0` when the customer chose a zero excess — which is almost certainly what you want,
  but it is not what a JavaScript `||` would do.
- **Expecting `[]` or `{}` to be skipped.** Only `null` and the empty string are skipped. An
  empty array is a value.
- **Assuming `fetch(path, default)` behaves the same.** It does not skip a present-but-null:
  `=fetch($.a,'-')` on `{ "a": null }` returns `null`, while `=coalesce($.a,'-')` returns `'-'`.
- **Putting the cheapest candidate last.** Evaluation is left to right and stops at the first
  winner, so order the arguments by preference — the first match is the answer, not the best match.
- **Path args resolve against the document root.** Inside the call, use `$.field`.
