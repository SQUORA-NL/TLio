# Missing functions — what the sample scripts had to work around

> **Status: all 21 delivered.** This document is kept as the record of *why* each function
> exists and what it replaced. Every function named below is implemented, registered, tested,
> documented under `docs/ai-ref/functions/`, and exercised in all three formats by
> `TLio.Parity.Tests`. The two rating samples were rewritten onto them and produce
> **byte-identical output** — the functions changed the writing, not the rating.

Read from the two shipped rating samples (`docs/samples/car-insurance-nl/native` and
`.../sivi-afd`, 570 and 552 lines) and the showcase corpus. Every entry below was justified by
an expression that existed in those files, or by a hole with no workaround at all.

The goal was **not** to remove nesting — nesting is how the language composes. The goal was to
stop *one idea* from costing four levels. Section "Not proposed" says which combinations were
considered and rejected; those were not built, and should not be.

Surface before: 68 documented functions + 8 undocumented Math ones
(`subtract`, `calculate`, `sumifs`, `countifs`, `averageif`, `averageifs`, `minifs`, `maxifs`).
Surface after: **98 registered names** (97 implementations — `path` aliases `scriptPath`).

## What landed, and what it cost the samples

| | Before | After |
|---|---|---|
| Driver age | 6 calls, 4 levels deep, ×3 per script | `=dateDiff(birthDate,quotedOn,'years')` |
| WA premium | `calculate(concat(a,'*',b,…))` through `DataTable.Compute` | `=round(=multiply(a,b,c,d,e,f),2)` |
| Renewal date | year sliced out, incremented, glued back on | `=dateAdd(startDate,1,'years')` |

20 expressions rewritten across the two samples; both outputs unchanged.

---

## Tier 1 — each one collapses an idiom that appears repeatedly in the samples

### 1. `dateDiff(from, to, unit)`

The single worst expression in the repo. Both samples compute driver age, licence years and
vehicle age this way — six calls, four levels deep, three times per script:

```jsonc
// native/script.json:242
"=floor(=calculate(=concat('(',=replace($.request.quotedOn,'-',''),'-',=replace($.request.applicant.birthDate,'-',''),')/10000')))"
```

That is not date arithmetic, it is YYYYMMDD subtraction divided by 10000 — it happens to give
the right whole-year answer and gives a wrong one the moment anyone asks for months.

```jsonc
"=dateDiff($.request.applicant.birthDate,$.request.quotedOn,'years')"
```

- `unit`: `years | months | weeks | days | hours | minutes | seconds`
- Whole units, truncated toward zero — `years` must mean "birthdays passed", not `days/365.25`,
  because that is what every age-based rule in the samples means.
- Negative when `to` precedes `from`; pair with the existing `abs` when the sign is unwanted.
- Sits in `TLio.Extensions.TimeDate`, next to `dateCompare`.

**Replaces:** 6 expressions across the two samples, 4 levels → 1.

### 2. `multiply(a, b, …)` and `divide(a, b)`

`sum` is variadic and `subtract` exists, but there is no multiply and no divide. Every premium
factor chain therefore round-trips through a *string*:

```jsonc
// native/script.json:401
"=round(=calculate(=concat($.calc.lookup.waBasePremium,'*',$.calc.lookup.regionFactor,'*',$.calc.ageFactor,'*',$.calc.mileageFactor,'*',$.calc.lookup.usageFactor,'*',$.calc.lookup.bmWaFactor)),2)"
```

```jsonc
"=round(=multiply($.calc.lookup.waBasePremium,$.calc.lookup.regionFactor,$.calc.ageFactor,$.calc.mileageFactor,$.calc.lookup.usageFactor,$.calc.lookup.bmWaFactor),2)"
```

`calculate` builds a `DataTable` and parses an expression per invocation, and inherits
`DataTable.Compute`'s locale and precision rules — that is a lot of machinery to multiply six
numbers. The casco line needs `divide` too:

```jsonc
// before — note the '/12*' welded into the middle of the string
"=round(=calculate(=concat($.request.vehicle.currentValue,'*',$.calc.lookup.cascoRate,'/12*',...)),2)"
// after
"=round(=divide(=multiply($.request.vehicle.currentValue,$.calc.lookup.cascoRate,...),12),2)"
```

- `multiply` variadic, mirroring `sum`; `divide` binary, mirroring `subtract`.
- `divide` by zero fails the function (it aborts the script, per B2) rather than emitting `∞`.

**Replaces:** 6 expressions, 3 levels → 2. Leaves `calculate` for genuinely free-form formulas.

### 3. `if(condition, whenTrue, whenFalse)`

There is a conditional *command* (`ifElse`) but no conditional *value*. Producing one field two
ways therefore costs a nested script block:

```jsonc
// 5 lines of command JSON to set one number
{ "command": "ifElse", "condition": "=equals($.calc.grantedCover,'casco')",
  "then": [ { "command": "put", "path": "$.calc.casco", "value": "…" } ],
  "else": [ … ] }
```

```jsonc
{ "command": "put", "path": "$.calc.excess",
  "value": "=if(=lessThan($.calc.driverAge,24),=sum($.request.cover.voluntaryExcess,300),=fetch($.request.cover.voluntaryExcess))" }
```

Both samples use `ifElse` four times where the body is a single `put`. This does not replace
`ifElse` — structural branching (adding a clause object, removing a node) still needs the
command. It replaces `ifElse`-as-ternary.

- Lazy: the branch not taken is not evaluated, so `=if(=exists($.a),=fetch($.a),'-')` is safe.

### 4. `dateAdd(date, amount, unit)`

The renewal date is currently computed by slicing the year out of a string, adding one to it,
and gluing the rest back on:

```jsonc
// native/script.json:450
"=concat(=sum(=substring($.request.requestedStartDate,0,4),1),=substring($.request.requestedStartDate,4,6))"
```

```jsonc
"=dateAdd($.request.requestedStartDate,1,'years')"
```

Same unit vocabulary as `dateDiff`. Negative amounts subtract. Month-end clamps (31 Jan + 1
month → 28/29 Feb), which the string trick cannot do at all.

---

## Tier 2 — real holes, less frequent in these two samples

### 5. `coalesce(a, b, c, …)`

`fetch(path, default)` gives exactly one fallback. Three candidate sources — routine in SIVI/AFD
mapping, where the same field lives under different party roles — means nesting `fetch` inside
`fetch`. `coalesce` returns the first argument that is not null and not empty, variadic.

### 6. `datePart(date, part)`

`part`: `year | month | day | hour | minute | second | quarter | dayOfWeek | weekOfYear |
daysInMonth`. Returns a number. Kills the `=substring($.date,0,4)` idiom (used in both samples)
and the string-index assumptions that come with it.

### 7. `formatDate(date, format)` and `parseDate(text, format)`

`datetime(format)` can format *now* and nothing else. There is no way to render a stored date,
and no way to read a non-ISO one — a `dd-MM-yyyy` input can only be normalised with
`split`/`concat` surgery. These two close the loop with the existing `datetime`.

### 8. `between(value, low, high)`

`isDateBetween` exists for dates; the numeric sibling does not, so every band check in the rate
tables is `=and(=greaterOrEqual(v,lo),=lessOrEqual(v,hi))`. Inclusive on both bounds, matching
`isDateBetween` exactly.

### 9. `distinct(array)`

No workaround exists today — not with JSONPath, not by composing existing functions. Dedupe is
currently only reachable through `merge` with `uniqueItemsWithoutKeys`, which needs a second
document to merge with.

### 10. `sort(array, 'asc'|'desc')` and `sortBy(arrayPath, keyPath, dir)`

Also has no workaround. `minDate`/`maxDate`/`min`/`max` answer the one-value question; ordering
a collection for output is unreachable. `sortBy` is the one that matters for reporting
(coverages by premium, claims by date).

---

## Tier 3 — nice, but each saves one level at most

| Function | Today | Note |
|---|---|---|
| `clamp(v, lo, hi)` | `=min(=max(v,lo),hi)` | Common when capping a rating factor. |
| `last(path)` | `partial(path, n)` counts from the front only | Needs the length to reach the end. |
| `regexReplace(s, pattern, repl)` | `replace` is literal only | `matches` proves regex is already in the box, but only as a test. |
| `regexExtract(s, pattern, group?)` | not expressible | Postcode / licence-plate / IBAN splitting. |
| `right(s, n)` | `=substring(s,=subtract(=length(s),n),n)` | `left` is just `substring(s,0,n)` — not worth a function. |
| `startOfMonth(d)` / `endOfMonth(d)` | not expressible (month lengths) | Term boundaries. |
| `abs`-style `sign(v)` | `=if(=lessThan(v,0),-1,1)` once `if` lands | Low value; listed for completeness. |

---

## Not proposed — deliberately

- **Rounded variants of anything.** No `roundedMultiply`, `sumRounded`, `avgTo2`. Rounding is a
  separate decision from the arithmetic, and folding it in multiplies the surface by the number
  of rounding modes for no expressive gain. `=round(=multiply(…),2)` is two levels and reads
  correctly.
- **`switch` / `lookup`.** The `decisionTable` command already does keyed multi-way selection,
  with priority and defaults, and does it better than an argument list would.
- **`filter(array, predicate)`.** JSONPath filter expressions (`$.items[?(@.type=='wa')]`)
  already cover this for JSON, and XPath predicates cover XML. It *is* a real gap for the YAML
  dot-notation fetcher — but that is a fetcher gap, to be fixed there, not a function.
- **`map` / `reduce`.** The engine is command-driven; per-element transformation belongs to a
  command over a wildcard path, not to a function returning a collection.
- **`concatIf`, `sumIfEmpty`, `defaultTo`-style pairs.** Once `if` and `coalesce` exist these
  are compositions, and composition is what the language is for.
- **`now()` / `today()`.** `datetime(format)` already covers it.

---

## What building them turned up

Three things worth keeping, because they were not visible from the analysis:

- **`$1` was unusable as a regex replacement.** `FunctionBase.ResolveArg` re-reads any resolved
  argument that *looks* like a path, and in JSON/YAML a path starts with `$` — exactly how .NET
  substitution syntax starts. `=regexReplace($.p,'…','$1 $2')` handed `$1 $2` to the JSONPath
  fetcher, which threw out of `Execute`. The Text pack now has a narrow `TryGetRegexStringArg`
  that treats `$` as a path only when the next character is `.` or `[`. Any future function
  taking a literal `$` argument will hit the same edge.
- **`between` could not copy `isDateBetween`.** A core predicate answers `false` for a path that
  matches nothing; the TimeDate pack fails. Both are right where they live, so the two range
  predicates disagree on purpose — recorded as B3 in `docs/behaviour-decisions.md`.
- **`sortBy` keys are property chains, not path expressions.** `SelectNodes(path, element)` is
  node-relative for JSON and YAML but not for XML, where it evaluates against the document. A
  per-format answer would have been worse than a narrower one.

## The rule for the next function

Anything landing here needs an entry in `docs/ai-ref/functions/`, a row in
`docs/ai-ref/guide.md`, and a line in **both** `TLio.Parity.Tests/Sweep/sweep.json` and
`Sweep/sweep.xml` — the sweep asserts that every registered function is exercised in every
format, and it will fail until you do. Re-record with
`dotnet test TLio.Parity.Tests --filter "Name~RecordSweep"` and read the diff; recording
captures whatever happened, including a wrong answer.
