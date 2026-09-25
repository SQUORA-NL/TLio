# while

> Repeats a nested script while a condition holds — for when even the number of iterations
> isn't known until you compute it (walking a date cycle, accumulating until a threshold).

## Syntax

```json
{
  "command": "while",
  "condition": <TLioValue>,
  "commands": [ <nested commands> ],
  "maxIterations": <int, required>
}
```

> See [Notation Reference](../notation-reference.md) for quoting rules and escape sequences.

## Options

| Option | Type | Required | Default | Description |
|--------|------|----------|---------|-------------|
| condition | TLioValue | yes | — | Re-evaluated for truthiness before every pass, including the first. Same truthy rules as `ifElse`. |
| commands | array | yes (no-op if empty) | — | Nested script executed each pass. |
| maxIterations | int | **yes** | — | Hard cap on iterations. No default — see **Why maxIterations has no default**. |

**Functions in the value**: ✅ condition

## Formats

Works with all adapters. Path syntax differs per adapter — see [overview.md](../overview.md).

## Why maxIterations has no default

An unbounded `while` over a live document is a real hang risk — a condition that never goes
false locks up the request that runs it. This is the one place this package insists a caller
think about a bound instead of getting one for free. Reaching the cap logs a warning and stops
the loop — it is not a script failure.

## No per-pass "current element"

Unlike `forEach`, `while` doesn't set `IExecutionContext.CurrentNode` itself, so a `while` body
works against whatever absolute, evolving state its own commands choose to read and write
(advancing a scratch "next date" field with `dateAdd`, for example). `@` still works inside a
`while` nested inside a `forEach`, though — it reaches the enclosing loop's current element,
since nothing here clears it.

## Building a list while repeating

`while` has no `appendTo`, for the same reason `forEach` doesn't (see
[ForEach.md](ForEach.md#building-a-second-array-while-iterating)): `add` needs a literal "next
free" index, which a loop body has no way to compute for itself. Build a delimited string
instead, then `split()` it once after the loop:

```json
[
  { "command": "add", "path": "$.datesCsv", "value": "" },
  { "command": "add", "path": "$.cursor", "value": "$.start" },
  { "command": "while",
    "condition": "=lessOrEqual($.cursor, $.end)",
    "maxIterations": 1000,
    "commands": [
      { "command": "set", "path": "$.datesCsv",
        "value": "=concat($.datesCsv, =if(=equals($.datesCsv,''),'',','), $.cursor)" },
      { "command": "set", "path": "$.cursor", "value": "=dateAdd($.cursor, 1, 'years')" }
    ] },
  { "command": "add", "path": "$.schedule", "value": "=split($.datesCsv, ',')" }
]
```

The `if(equals($.datesCsv,''),'',',')` guard is what keeps the first entry from getting a
leading separator — without it, `split` produces a leading empty element.

## Verified example

Input: `{ "start": "2025-01-01", "end": "2027-01-01" }`
Output includes: `"schedule": ["2025-01-01", "2026-01-01", "2027-01-01"]`

Verified by: `TLio.UnitTests/CommandsTests/LoopingTests/WhileTests.cs`; the full idiom above is
exercised end-to-end by `samples/TLio.Sample.Actus.Api/Scripts/pam-simple.json`.

## When to use

- Walking a cycle forward (or backward) until a computed boundary — a payment schedule, a
  cursor that advances by a variable step each pass.
- Accumulating a running total until it crosses a threshold, without knowing in advance how many
  passes that takes.

## When NOT to use

- The number of iterations is exactly the length of an existing array — use `forEach`, which
  also gives you per-element addressing (`@`) for free.
- A single yes/no branch with no repetition — use `ifElse`.

## Common mistakes

- **Omitting `maxIterations`.** It's required precisely because it has no safe default —
  validation fails the command rather than silently defaulting to something that could hang.
- **A condition that never changes.** If nothing inside `commands` affects what the condition
  reads, the loop either never runs (condition starts false) or always hits `maxIterations`
  (condition never turns false) — there is no in-between.
- **Expecting the condition to be re-evaluated *after* the last pass to decide whether to stop
  early inside that pass.** It's checked *before* each pass, including whether to run a pass at
  all — a condition already false skips `commands` entirely, matching `ifElse`'s truthy rules.
- **Using `$.__loop` or `appendTo`.** These were an earlier, rejected design — build lists with
  the delimited-string idiom above instead.

## Failure modes and what the trace tells you

| Situation | Trace outcome | What happened |
|---|---|---|
| Condition false immediately | `success`, 0 iterations | The body never ran. |
| Condition never went false | `success` (stopped at cap) | A warning reports the loop was stopped at `maxIterations`. |
| Missing `condition` or non-positive `maxIterations` | `failure` | Validation failed before anything ran. |
| A nested command failed | `failure` | The loop stops at that pass, mirroring how a top-level script stops at its first failing command. |
