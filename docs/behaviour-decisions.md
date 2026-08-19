# Behaviour decisions to make

Things the test sweep turned up that are **currently unspecified rather than decided**. Each one
is pinned by a test asserting today's behaviour, so nothing changes silently — and each test says
"if this ever returns X, the decision has been made" so the pin is easy to find when you change it.

None of these need to match JLio. They do need to be *chosen*, and most of them are natural
candidates for a settings object rather than a hard-coded rule.

---

## A. Silent wrong answers

The worst class: no warning, no failure, just a different number than the author meant.

### A1. `"3,5"` becomes `35` in every math function

A European decimal read as a thousands separator — a tenfold error from data that looks fine.

```json
{"command":"put","path":"$.out","value":"=abs($.n)"}   // {"n":"3,5"} → 35
```

Pinned: `MathContractTests.ACommaDecimalString_IsReadAsThousandsSeparatedAndBecomesThirtyFive`

**Sharper still:** `=calculate('2,5+3,7')` *fails* on the same notation. Two functions in the same
pack give opposite answers to the same input. Whatever the rule is, it should be one rule.

**Is `.` actually the standard?** Yes, and there is no alternative to support:

| Format | Rule |
|---|---|
| JSON | RFC 8259 §6 — `decimal-point = %x2E`, the period. No locale mechanism exists in JSON. |
| XML | XML Schema `xs:decimal` / `xs:double` use the period. |
| YAML 1.2 | Core-schema numbers are JSON-compatible. |

So a *native* number can never carry a comma in any format TLio reads — `{"n": 3,5}` is a
syntax error, not a European number. The ambiguity exists **only** for numbers carried as
strings (`{"n": "3,5"}` is valid JSON), and nothing in a document declares which locale wrote
it. Interpretation is therefore application-defined, which makes it a setting rather than a
standard to follow.

Documented in `docs/ai-ref/notation-reference.md` §2 and pinned by
`DecimalSeparatorTests`.

Setting shape: string-to-number parsing — `Strict` (period only, comma rejected) /
`ThousandsGrouping` (today) / `CommaDecimal`.

### A2. `true` counts as `1`

```json
"=sum($.values)"   // {"values":[1,true]} → 2
```

A boolean stirred into a total shifts it with no warning. Strings are rejected as non-numeric;
booleans are not.

Pinned: `MathContractTests.ABooleanIsSilentlyCountedAsOne`

Setting shape: numeric coercion — which JSON types are accepted as numbers.

### A3. A path-valued index in `=partial()` is ignored

```json
"=partial('$.items[*].n', $.which)"   // which=2 → returns element 0, not element 2
```

The index is read with `TryGetDouble` on the raw argument, so `"$.which"` is a non-numeric string
and the index quietly stays at its default. A data-driven index returns the wrong element.

Pinned: `PartialDepthTests.AnIndexGivenAsAPath_IsNotResolvedAndSilentlyDefaultsToZero`

### A4. A path-valued default in `=fetch()` is not resolved

```json
"=fetch($.missing, $.order.email)"   // → the literal string "$.order.email"
```

Only the first argument goes through path detection.

Pinned: `FetchDepthTests.ADefaultGivenAsAPath_IsNotResolved`

### A5. `=scriptpath(argument)` always answers `"$"`

Both the absolute form and the parent-relative form collapse to the root:

| Form | Returns | Expected |
|---|---|---|
| `=scriptpath()` | `$.items[0]` ✅ | correct |
| `=scriptpath('@.n')` | `$.items[0].n` ✅ | correct |
| `=scriptpath($.address)` | `$` ✗ | `$.address` |
| `=scriptpath('@.<--')` | `$` ✗ | `$.items` |

Only arguments starting with `@` go through `ResolveRelativePath`; anything else is used as the
subject node directly, and a node resolved that way has no parent chain for `GetPath` to walk.

Pinned: `ScriptPathDepthTests.AnAbsolutePathArgument_ReportsTheRootRatherThanThatPath`,
`ScriptPathDepthTests.TheParentRelativeForm_AlsoReportsTheRoot`

### A6. Non-finite arithmetic results serialise as strings

```json
"=calculate('5/0')"   // → "Infinity"  (a JSON string)
"=calculate('0/0')"   // → "NaN"
```

JSON has no infinity. A consumer reading `$.result` as a number gets a string instead.

Pinned: `CalculateTests.DivisionByZero_ProducesANonFiniteNumber`,
`CalculateTests.ZeroDividedByZero_ProducesNaN`

Setting shape: non-finite results — `Error` / `Null` / `String` (today).

### A8. `resolve` key paths root `$` at the target, not the document

`Resolve.GetValues` sends a non-`@.` key path to `SelectNodes(path, token)` with the **target
node** as the root. So in a rule over `$.items[*]`, a key path of `$.wanted` looks for a
`wanted` property inside `$.items[0]` — not at the document root.

That is the opposite of the framework's own stated rule ("Function path resolution uses
document ROOT", `TLio_AI_Reference.md` §1), so a key path that reads as a document lookup
silently matches nothing.

Pinned: `ResolveDepthTests.AnAbsoluteKeyPathIsEvaluatedAgainstTheTargetNode_NotTheDocumentRoot`


---

## B. Inconsistent failure semantics

Same situation, different answer depending on where you hit it.

### B1. `count` answers `0` for a missing path; every other aggregate fails

`sum`, `avg`, `min`, `max`, `median` all fail on a path that matches nothing. `count` returns `0`.
"Zero items" and "I could not find that" are different facts, and a caller cannot tell them apart.

Pinned: `MathContractTests.CountAnsersZeroForAMissingPath_UnlikeEveryOtherAggregate`

### B2. A failed *function* aborts the script; a missing *path* does not

`TLioScript.Execute` breaks on the first failed command. So:

| Situation | Result |
|---|---|
| `set` on a path that matches nothing | warning, script continues |
| `=sum($.nowhere)` in a value | command fails, **script stops** |

Both are "the document did not have what the script expected", and they behave differently.

Pinned: `IndirectDepthTests.AFailedIndirect_AbortsTheRestOfTheScript`,
`MathContractTests.AFailedMathFunction_AbortsTheRestOfTheScript`

Setting shape: on-step-failure — `Abort` (today) / `ContinueAndCollect`.

### B3. ETL commands do not resolve `=indirect()` in their paths

Every core command now resolves `=indirect(...)` in **every** path it takes. `flatten`, `restore`,
`resolve` and `tocsv` do not — they fail (without throwing) instead.

Documented in `docs/ai-ref/commands/Flatten.md` and siblings.

---

## C. Deliberate divergences from JLio

Recorded so they read as choices rather than gaps.

| Behaviour | JLio | TLio |
|---|---|---|
| `{{$.path}}` substitution inside `=calculate()` | supported | not supported — build the expression with `=concat()` or store it in the document |
| Comma decimals in `=calculate()` | accepted | rejected (see A1 for the inconsistency with the math pack) |
| JSchema pack (`filterBySchema`, `orderBySchema`) | present | absent — deferred in `specs/002-migration-from-jlio/spec.md`; the schema library JLio uses is AGPL / paid and Newtonsoft-bound |

---

## D. Test coverage

Every function and command pack is now at or above JLio's `[Test]`/`[TestCase]` density.
The two areas that were behind have been closed:

| Area | JLio | TLio before | TLio now |
|---|---|---|---|
| `calculate` | 58 | 3 | 61 |
| math null / type contract | 39 | 11 | 40 |
| `resolve` | 24 | 8 | 30 |
| `flatten` / `restore` | 16 | 11 | 40 |
| `indirect` | 23 | 6 | 51 |
| `fetch` | 20 | 10 | 32 |
| `partial` | 20 | 6 | 23 |
| `scriptPath` | 16 | 4 | 15 |

---

## E. Cross-format behaviour still not aligned

The XML/YAML alignment work (020) brought the three adapters onto one data model — see
`docs/ai-ref/adapters/document-shape.md` — and `TLio.Parity.Tests` now runs one fixture corpus
against all three. These are what it does **not** cover, because they are decisions rather than
bugs.

### E1. The empty XML element cannot say which empty thing it is

`<k/>` is `null`, `""`, `{}` and `[]` at once, and no attribute-free encoding separates them.
Each predicate answers its own question and `GetNodeKind` settles on `Null` (see the adapter
remarks). The visible consequence: `remove` emptying an object leaves `{}` in JSON and `<a/>`
in XML, and re-reading that XML gives `null`.

Pinned: `XmlShapeTests.AnEmptyElement_IsAlsoNull_BecauseXmlCannotTellTheTwoApart`

Setting shape: an explicit type marker (`xsi:nil`, or a TLio-owned attribute) — which means
deciding that attributes are in scope for the data model, currently they are not.

### E2. A single-element XML array is indistinguishable from a one-property object

`<items><item>1</item></items>` reads as an array only because the item is named `item`. In a
document TLio did not write, `<lines><line>1</line></lines>` is a one-property object.

Pinned: `XmlShapeTests.ASingleItemElement_IsAOneElementArray`,
`XmlShapeTests.AnObjectWithOneProperty_IsAnObjectNotAOneElementArray`

Setting shape: a configurable item name per array path, or a document-level convention.

### E3. Writing through an array subscript does not work in any format

`set $.items[1]` keeps `items[1]` as the leaf name, matches no property, and no-ops with a
warning. `put $.items[0]` goes further in JSON and creates a property literally named
`items[0]`. All three formats share the first behaviour, so it is not a divergence — but it is
also not what the path says.

Pinned: `TLio.Parity.Tests/Fixtures/Arrays/01-set-element-by-index-is-a-noop`

Setting shape: teach `SplitParentAndLeaf` that a trailing subscript addresses a position rather
than naming a property. This is a command-layer change, deliberately out of scope for 020.

### E4. A bare path is not a value in XML

`"value": "$.a"` is a path expression in JSON. In XML `/order/a` written as text stays text,
because a leading `/` is not distinctive enough to override at parse time, where no fetcher is
available to ask. `=fetch(/order/a)` works — path detection inside function arguments *is*
format-aware (`IItemsFetcher.IsPathExpression`).

### E5. `decisionTable` config is JSON-notation only

The XML and YAML script parsers convert settings objects through their generic POCO path, which
`DecisionTableConfig<TNode>` (generic, with node-typed members) does not go through.

### E6. A quoted YAML `'null'` is still read as null downstream

The script parser honours the quoting and writes the four-character string, but
`YamlNodeAdapter.IsNull` tests the text rather than the scalar style, so every function that
asks still sees null. Telling them apart end to end needs a styled scalar in the adapter's
value model.

Pinned: `YamlScriptNotationTests.AQuotedNullValue_IsParsedAsTheString`

---

## Resolved

Findings from the same sweep that were plain bugs rather than decisions, and have been fixed.

### `flatten` → `restore` lost arrays of scalars

```
["a","b","c"]  →  [{"0":"a"},{"1":"b"},{"2":"c"}]     (before)
[[1,2],[3,4]]  →  [{"0":1,"1":2},{"0":3,"1":4}]       (before)
```

`Restore.SetNestedValue` consumed an array index inside the parent's branch and then fell
through to `SetProperty(current, path[^1], value)` — so a **trailing** index became a property
name. Arrays of objects were unaffected, because a named property follows the index there; a
bare scalar has nothing after its index, and that was the case that broke.

The walk now advances one segment at a time and decides at the final segment whether it is an
array position or a property name. The same defect in the no-metadata best-effort path
(`SetNestedValueBasic`, which created the array but never descended into it) is fixed alongside.

Regression guards: `FlattenRestoreDepthTests.RoundTrip_ArrayOfScalars`,
`RoundTrip_NestedArrays`, `RoundTrip_ArrayOfScalars_KeepsElementTypes`,
`RoundTrip_ScalarArrayBesideObjectArray`, `RoundTrip_IsExactForAWholeMixedDocument`,
`RestoreWithoutMetadata_RebuildsArraysFromNumericSegments`.

### `=indirect()` threw out of the engine

Remove, rename and copy/move's `FromPath` never resolved the expression, and every command fell
back to the raw path when resolution failed — so an unresolvable `=indirect()` threw a
`JsonException` out of `ScriptEngine.Execute`, taking the whole script with it. Merge and
compare threw for the same reason. All paths now resolve through
`TLio.Commands.Logic.IndirectPath`, and an expression that cannot be resolved warns and no-ops.

Regression guards: `IndirectPathTests` (27 cases).
