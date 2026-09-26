# forEach

> Executes a nested script once per element of an array — the primitive TLio was missing for
> "run this N times," where N is computed at runtime rather than known when the script is
> written. `@` addresses the element that iteration is on.

## Syntax

```json
{
  "command": "forEach",
  "path": "<array path>",
  "commands": [ <nested commands> ],
  "maxIterations": <int, optional>
}
```

> See [Notation Reference](../notation-reference.md) for quoting rules and escape sequences.

## Options

| Option | Type | Required | Default | Description |
|--------|------|----------|---------|-------------|
| path | string | yes | — | Path to the array node to iterate. Each element becomes one iteration. |
| commands | array | yes (no-op if empty) | — | Nested script executed once per element, with `@` addressing that element. |
| maxIterations | int | no | 100,000 | Safety cap on the number of elements processed. |

**Functions in the path**: ✅ `=indirect()`, and `@`/`@.field` when nested inside another `forEach`.

## Formats

Works with all adapters. Path syntax differs per adapter — see [overview.md](../overview.md).
XPath note: `@` is XPath's attribute-reference syntax, so XML's current-item token is the
ordinary XPath `.` instead — `path="."`, `path="./field"`.

## `@` addresses the current element

This is the *same* relative-path token `decisionTable`/`resolve`/`setProperties` already use
(`@.property`) — `forEach` just gives it something to anchor to for the duration of each pass:
`IExecutionContext.CurrentNode`, set to the element before running `commands` and restored
afterward (nesting works by ordinary call-stack scoping — the innermost active `forEach` is
what `@` reaches).

- **`path: "@"`** (bare) — the whole element. `set path="@" value="..."` replaces it in place;
  this is how a loop transforms an array without needing a second array to write into.
- **`path: "@.field"`** — one property of the element, when it's an object.
- **In a *value*** — `"value": "@.field"` (or nested in a function argument,
  `=fetch(@.field)`) — reads a field off the current element while writing anywhere else in the
  document (a running total elsewhere, say). This is `context.CurrentNode`, not the writing
  command's own resolved target, that a value like this anchors to — so it works even when the
  command's `path` has nothing "@"-relative about it at all.
- **Reading the whole element as a value** (a *bare* `@`, not `@.field`) is not supported —
  `=fetch(@)` does not work. `@`/`$`/`.` are ordinary characters in real data (a decimal point,
  a padding character, an "at" sign), and nothing at that point in parsing knows whether a
  single character was typed as a path or is quoted literal text — see
  `IItemsFetcher.IsPathExpression`'s remarks for the concrete case (XML's `.` colliding with
  `padLeft(...,'.')`) that ruled this out. Two ways around it, depending on what you're doing:
  - If the element is an object, always reach fields through `@.field` — never the bare
    element.
  - To read a *bare-scalar* element's own value (a plain string in the array, not an object),
    use `=fetch(=scriptpath())`: `scriptpath()` (bare) returns the current element's own
    absolute path as a freshly *computed* string, not literal text a script author typed, so
    `fetch(...)` re-resolving it is unambiguous. Capture it into a scratch field once at the top
    of the loop body if you need it more than once that pass — reading it again after a
    `set path="@" value=...` has already replaced the element would read the *replacement*, not
    the original.

## Building a second array while iterating

`forEach` has no `appendTo` — `add` only ever appends at a literal "next free" integer
subscript ([Add.md](Add.md#array-positions)), and a script cannot compute that subscript for
itself without knowing the array's length, which isn't something a loop body can safely track by
itself either. Two idioms cover the practical cases, and neither needs anything from `forEach`
beyond what any script already has:

- **Transform in place.** If the result belongs at the same position as the source (mapping an
  array to same-shaped output), just `set path="@" value="..."` — no second array needed at all.
  This is the common case.
- **Build a delimited string, then `split()`.** For "grow a list of scalars during a loop"
  (dates in a payment schedule, say), append to a plain string (`=concat($.csv, ',', @.field)`)
  during the loop, then `{"command":"add","path":"$.result","value":"=split($.csv, ',')"}`
  once after it. Works for `while` too — see [While.md](While.md#building-a-list-while-repeating).

## Verified example

```json
[
  { "command": "forEach", "path": "$.items", "commands": [
      { "command": "set", "path": "@", "value": "=multiply(@.n, 2)" }
  ] }
]
```

Input: `{ "items": [{"n":1}, {"n":2}, {"n":3}] }`
Output: `{ "items": [2, 4, 6] }` — each element replaced wholesale by its own doubled `n`.

Verified by: `TLio.UnitTests/CommandsTests/LoopingTests/ForEachTests.cs`,
`TLio.UnitTests/CommandsTests/LoopingTests/LoopAddressingTests.cs`.

## When to use

- The number of times to repeat a step depends on data, not on the script's own text — a
  computed schedule, a variable-length list of records.
- Transforming every element of an array the same way.
- Accumulating running state across a sequence read in order (a fold) — each iteration's `set`
  writes to a field the next iteration's functions read back, exactly like top-level commands
  already thread the document between them.

## When NOT to use

- The array's length is known when the script is written — just write the commands out.
- You need to repeat while a *condition* holds rather than once per array element — use `while`.
- You need a single declarative operation per matched node (a lookup, a rule table) — `resolve`
  or `decisionTable` already do that without a nested script.

## Common mistakes

- **Expecting `=fetch(@)` (bare) to read the whole current element.** It doesn't — see above;
  use `@.field` on an object element, or `=fetch(=scriptpath())` on a scalar one.
- **Reading `@`/a captured scratch field *after* `set path="@" value=...` has already replaced
  the element.** Capture anything you still need before that command runs, not after.
- **Using `$.__loop`, `appendTo`, or `$__loop[0].result`.** These were an earlier, rejected
  design — nothing in the current implementation reads them; use `@` and the idioms above.
- **Mutating the array named by `path` (inserting/removing elements) from inside the loop
  body.** Elements are addressed by a reference snapshotted before the loop starts; changing the
  array's length mid-loop leaves that snapshot stale.

## Failure modes and what the trace tells you

| Situation | Trace outcome | What happened |
|---|---|---|
| `path` matched nothing or wasn't an array | `noop` | Nothing changed; a warning names the path. |
| Array was empty, or `commands` was empty | `noop` | Nothing to iterate, or nothing to run. |
| More elements than `maxIterations` | `success` (truncated) | A warning reports the truncation; iteration stops early. |
| A nested command failed | `failure` | Iteration stops at that element, mirroring how a top-level script stops at its first failing command. |
