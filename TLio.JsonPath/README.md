# TLio.JsonPath

**Moving from Newtonsoft.Json to System.Text.Json and still need `SelectToken` / `SelectTokens`?** This is a
drop-in: the same JSONPath strings select the **same nodes, in the same order, failing in the same places**, on
`System.Text.Json.Nodes.JsonNode`. That is not a claim, it is a test: every query in the suite is run through
Newtonsoft.Json 13.0.4 and through this engine and the results must agree (≈1.8 million comparisons in the long soak).

Want the standard instead? The same engine reads **RFC 9535** in full — function extensions, I-Regexp, normalized paths —
and passes the official compliance suite (706 / 706). A third dialect, `Extended`, accepts both. No dependencies: not
Newtonsoft.Json, not JsonCons, not TLio. It is the JSONPath engine behind
[`TLio.Json.SystemText`](https://github.com/SQUORA-NL/TLio/blob/main/TLio.Json.SystemText/README.md), but it is a package in its own right:

```sh
dotnet add package TLio.JsonPath
```

Coming from Newtonsoft — the default dialect is yours, nothing to configure:

```csharp
using System.Text.Json.Nodes;
using TLio.JsonPath;

JsonNode doc = JsonNode.Parse(json)!;
var engine = JsonPathEngine.Default;                        // Newtonsoft's dialect

IReadOnlyList<JsonPathMatch> all = engine.Select("$.store.book[?(@.price < 10)].title", doc);   // ≈ SelectTokens
JsonPathMatch?               one = engine.SelectSingle("$.store.book[0].title", doc);            // ≈ SelectToken (>1 match throws)
```

Wanting RFC 9535:

```csharp
using System.Text.Json.Nodes;
using TLio.JsonPath;

var doc = JsonNode.Parse("""
    { "store": { "book": [ { "title": "Sayings", "price": 8.95 },
                           { "title": "Sword",   "price": 12.99 } ] } }
    """);

var engine = new JsonPathEngine(new JsonPathOptions { Dialect = JsonPathDialect.Rfc9535 });

foreach (var match in engine.Select("$.store.book[?@.price < 10].title", doc))
    Console.WriteLine($"{match.NormalizedPath} = {match.Node}");
// $['store']['book'][0]['title'] = "Sayings"
```

Selected nodes are the **live nodes of your document** — nothing is copied or serialized — so you
can read, replace or remove them through their `Parent`.

## Why there are dialects

The two JSONPaths in the wild disagree. Newtonsoft.Json's `SelectToken`/`SelectTokens` predates the
standard and is defined by its implementation; RFC 9535 is the standard. They differ in syntax
(`$.a-b`, `[?(…)]` vs `[?…]`), in results (`$..*`, filters over objects, `!=` on a missing member)
and in errors. One rule set cannot be both, so the dialect says which applies:

| Dialect | What it is | Use it when |
|---|---|---|
| `Newtonsoft` *(default)* | Behaves exactly as Newtonsoft.Json 13.0.4 `SelectToken(s)`: same syntax, same nodes in the same order, same errors | Scripts and paths written against Newtonsoft keep working unchanged on System.Text.Json |
| `Rfc9535` | The standard, nothing else — including function extensions and I-Regexp (RFC 9485) | You want standards-compliant behaviour |
| `Extended` | A strict superset of `Newtonsoft`: anything Newtonsoft accepts gives the Newtonsoft result; anything it rejects is read as RFC 9535 | You want Newtonsoft compatibility *and* `match()`, bare filters, double-quoted names, … |

The dialect is part of the engine's options. It is never a per-call switch and never global state.

## API

```csharp
var engine = new JsonPathEngine(options);              // thread-safe; keep one — it caches parsed queries

IReadOnlyList<JsonPathMatch> all = engine.Select(path, doc);   // every node, in order; empty is a valid result
JsonPathMatch?               one = engine.SelectFirst(path, doc);   // first match or null
JsonPathMatch?               only = engine.SelectSingle(path, doc); // Newtonsoft's SelectToken: >1 match throws
bool                          any = engine.Exists(path, doc);

JsonPathQuery q = engine.Parse(path);   // immutable, thread-safe; throws JsonPathException(Syntax) with a position
q.Select(doc);
```

A `JsonPathMatch` carries `Node`, `Parent`, `Name` (object member) or `Index` (array element),
`IsRoot`, and `NormalizedPath` (RFC 9535 §2.7, e.g. `$['store']['book'][0]`).

**JSON `null` is a match, not an absence.** `System.Text.Json` stores JSON null as a C# `null`, so
`match.Node == null` means the query selected a null value, and `Parent` + `Name`/`Index` is the
slot it sits in. A query that finds nothing returns an empty list. That is how
`{"a": null}` / `$.a` and `{}` / `$.a` are told apart.

`NormalizedPath` is computed on first use, by one walk of the document shared by all matches of
the same selection; selecting itself records only each match's last step and allocates no paths.

### Options

| Option | Default | Meaning |
|---|---|---|
| `Dialect` | `Newtonsoft` | The language queries are read in |
| `RegexTimeout` | 2 s | Cap on any single regex evaluation (`=~`, `match()`, `search()`); `Timeout.InfiniteTimeSpan` disables. Exceeding it raises `JsonPathException(Limit)` |
| `MaxQueryLength` | 4096 | Longest query accepted; longer fails immediately as `Limit`, unparsed |
| `MaxDepth` | 512 | Deepest nesting accepted, both of a query's filter expressions and of the document while `..` walks it. Both fail as `Limit`, never as a stack overflow |
| `ErrorWhenNoMatch` | `false` | Newtonsoft's `JsonSelectSettings.ErrorWhenNoMatch` (Newtonsoft/Extended dialects) |
| `EmulateNewtonsoftDates` | `true` | Newtonsoft reads ISO-8601-looking strings as dates (`DateParseHandling.DateTime`), which changes how they compare. See [Dates](#dates) |
| `StrictIRegexp` | `false` | Read `^`/`$` in `match()` as the literal characters RFC 9485's grammar says they are. See [I-Regexp](#i-regexp-and-the-two-places-it-is-not-the-letter-of-the-rfc) |
| `Functions` | none | Custom function extensions (below) |
| `QueryCacheSize` | 1024 | Parsed queries kept per engine; `0` disables |

### Errors

Everything throws `JsonPathException` with a `Kind` (`Syntax`, `Evaluation`, `Conversion`, `Limit`,
`MultipleResults`, `NoMatch`) and, for syntax errors, the zero-based `Position` of the offending
character. An invalid query fails when it is **parsed**, not when it is run. An empty result is
never an error (RFC 9535 §2.1) unless you ask for it with `ErrorWhenNoMatch`.

## Supported syntax

| | Newtonsoft | RFC 9535 | Extended |
|---|---|---|---|
| Root, `.name`, `['name']`, `['a','b']`, `[0]`, `[0,2]`, `[a:b:c]`, `[*]`, `.*`, `..` | ✓ | ✓ | ✓ |
| Filter `[?(…)]` | ✓ (parentheses required) | ✓ | ✓ |
| Filter without parentheses `[?@.a]` | ✗ | ✓ | ✓ |
| `==  !=  <  <=  >  >=` | ✓ | ✓ | ✓ |
| `<>`, `===`, `!==`, `=~ /re/flags` | ✓ | ✗ | ✓ |
| `&&  \|\|  !` and parentheses | `&&` `\|\|` only | ✓ | ✓ |
| `length() count() match() search() value()`, custom functions | ✗ | ✓ | ✓ (RFC-parsed queries) |
| Double-quoted names, mixed selectors in one bracket, `\t \n \r` as whitespace | ✗ | ✓ | ✓ (RFC-parsed queries) |
| Unquoted names with any character but `. [ ( space` (`$.a-b`, `$.1a`), and `[01]` | ✓ | ✗ | ✓ |
| `@` / `$` inside filters | ✓ | ✓ | ✓ |

**Extended, exactly.** The query text is first read as Newtonsoft; if that succeeds, Newtonsoft
semantics apply. If Newtonsoft rejects the text it is read as RFC 9535 and RFC semantics apply.
One refinement: Newtonsoft's negative array index (`[-1]`) never worked — it escapes as an
`ArgumentOutOfRangeException` — so there is no Newtonsoft answer to preserve, and Extended counts
from the end as RFC 9535 does. A query neither dialect accepts reports both reasons.

## Where the dialects differ

Each row is an executable test (`DivergenceTableTests`); the Newtonsoft column is additionally
checked against Newtonsoft.Json 13.0.4 itself.

| Rule | Newtonsoft | RFC 9535 | Extended | Why |
|---|---|---|---|---|
| `$.a-b` (unquoted `-`) | member `a-b` | syntax error | as Newtonsoft | RFC §2.5.1.1 shorthand names are `name-first *name-char` |
| `$.1a` (unquoted, leading digit) | member `1a` | syntax error | as Newtonsoft | same |
| `$["a"]` | syntax error (only `'` quotes) | member `a` | as RFC | RFC §2.3.1.1 allows both quotes |
| `$[01]` | index 1 | syntax error | as Newtonsoft | RFC `int` forbids leading zeros |
| `$[?@.a]` | syntax error | filter | as RFC | parentheses are optional in RFC §2.3.5 |
| `$[?(@.s =~ /^a/)]` | .NET regex | syntax error | as Newtonsoft | the RFC has `match()`/`search()` with I-Regexp instead |
| `$[?match(@.s,'a.*')]`, `length()`, `count()`, `value()` | syntax error | ✓ | as RFC | function extensions are RFC §2.4 |
| `$[?(@.n === 1)]` | strict equality | syntax error | as Newtonsoft | Newtonsoft-only operator |
| `$['a',0]` | syntax error | both selectors | as RFC | RFC allows mixed selector lists |
| `$[⇥'a']` (tab) | syntax error (only U+0020) | ✓ | as RFC | RFC §2 whitespace is space, tab, LF, CR |
| `$.o[?(@.a)]` — filter over an **object** | **nothing**: Newtonsoft walks the `JProperty` wrappers, and `@.a` on a wrapper finds nothing | members whose value has `a` | as Newtonsoft | RFC §2.3.5.2: a filter tests each child of an array *or object* |
| `$.arr.*` | nothing (`.*` is for objects) | the elements | as Newtonsoft | RFC §2.3.2: wildcard selects every child |
| `$.o[*]` | nothing (`[*]` is for arrays) | the members | as Newtonsoft | same |
| `$..*` | includes the **root** | descendants only | as Newtonsoft | RFC §2.5.2: descendants of the root, not the root |
| `$..a` order | matching members in document order | a node's own `a` before its descendants' | as Newtonsoft | RFC §2.5.2.2 visits nodes, then applies selectors |
| `$..[0]` | the `..` is silently dropped: same as `$[0]` | index 0 of every array at any depth | as Newtonsoft | Newtonsoft only honours `..` before a name or a filter |
| `$[-1]` | `ArgumentOutOfRangeException` | last element | last element | RFC §2.3.3.2; Newtonsoft never guarded the lower bound |
| `$[::0]` | error (`Step cannot be zero`) | empty | as Newtonsoft (error) | RFC §2.3.4.2.2 |
| `$[?(@.x != 1)]`, x missing | not selected (no left operand) | selected (`Nothing != 1`) | as Newtonsoft | RFC §2.3.5.2.2 |
| `$[?(@.v < 10)]`, `v` = `"7"` | selected: the string is converted to a number | not selected (different types never compare) | as Newtonsoft | Newtonsoft converts with `Convert.ToInt64` |
| `$[?(@.v < 10)]`, `v` = `"abc"` | **throws** `FormatException` (the whole query fails) | not selected | as Newtonsoft | same |
| `$[?(@.d == '2020-01-01T00:00:00.50Z')]`, `d` = that string | not selected — `d` is a *date*, written back as `…00.5Z` | selected | as Newtonsoft | see [Dates](#dates) |
| `a == 1 && b == 2 \|\| c == 3` | parsed as `a == 1 && (b == 2 \|\| c == 3)` | `(a == 1 && b == 2) \|\| c == 3` | as Newtonsoft | Newtonsoft's chain parser nests to the right; RFC §2.3.5.1 gives `&&` precedence |
| String order | by UTF-16 unit | by Unicode scalar value | as Newtonsoft | RFC §2.3.5.2.2 |
| `SelectSingle` with several matches | throws `MultipleResults` | throws `MultipleResults` | throws | the single-node API is Newtonsoft's `SelectToken`; use `SelectFirst` to take the first |

Slices `[start:end:step]` are the same in both dialects for every start, end and step in
\[−4, 4] ∪ {omitted} on arrays of length 0–5 (`DivergenceTableTests` compares all of them),
except `step == 0`.

## Function extensions

`length()`, `count()`, `match()`, `search()` and `value()` are built in. Static typing (RFC §2.4.3)
is enforced when a query is parsed: `$[?length(@.*) > 1]` is rejected up front because
`@.*` is a node list where `length()` takes a value.

Register your own with declared parameter and result types:

```csharp
var registry = new JsonPathFunctionRegistry().Register(new JsonPathFunction(
    "ends_with",
    [JsonPathFunctionType.Value, JsonPathFunctionType.Value],
    JsonPathFunctionType.Logical,
    args => JsonPathFunctionResult.FromLogical(
        args.Value(0).Node is JsonValue s && args.Value(1).Node is JsonValue suffix
        && s.GetValue<string>().EndsWith(suffix.GetValue<string>(), StringComparison.Ordinal))));

var engine = new JsonPathEngine(new JsonPathOptions { Dialect = JsonPathDialect.Rfc9535, Functions = registry });
engine.Select("$[?ends_with(@.name, '.pdf')]", doc);
```

Built-in names cannot be replaced. Registrations are snapshotted when the engine is created.

## I-Regexp, and the two places it is not the letter of the RFC

`match()` and `search()` accept **I-Regexp** (RFC 9485) only. A pattern is validated against the
I-Regexp grammar and then translated to an equivalent .NET `Regex`; one that is not I-Regexp
(`\d`, lazy quantifiers, back-references, …) never matches rather than quietly working. Matching is
by Unicode scalar value: `.` and negated classes consume a surrogate pair as one character.

* **`^` and `$`.** RFC 9485's grammar makes them ordinary characters, yet the JSONPath Compliance
  Test Suite expects `match(@, '^ab.*')` to anchor — as it does in every engine that hands the
  pattern to a regex library. By default a `^` that opens a branch and a `$` that closes one
  are anchors; elsewhere they are literal. `StrictIRegexp = true` gives the literal reading.
* **`\p{…}` and supplementary-plane characters.** .NET does not classify a character outside the
  BMP through `\p{L}` and friends, so a category escape never matches one.

## Dates

Newtonsoft.Json parses a string like `"2020-06-15T12:30:45+02:00"` into a *date value* the moment it reads the
document, and from then on it compares as a date. A System.Text.Json document still holds a string, so with
`EmulateNewtonsoftDates` (default `true`) the Newtonsoft dialect applies the same recognition at comparison time,
with Newtonsoft's own parser transcribed. What that means in practice:

| Comparison | Behaviour | Depends on the machine's time zone? |
|---|---|---|
| `==` / `!=` against a string, date stored as `…Z` or with no zone | the date is written back with Newtonsoft's ISO writer and compared as text, so `…00.50Z` is *not* equal to `'…00.50Z'` (it is written as `…00.5Z`) | **no** |
| `==` / `!=`, date stored with a numeric offset (`+02:00`) | the offset is converted to **local time** before writing back | **yes** |
| `<` `<=` `>` `>=` against a string | the string is converted with `Convert.ToDateTime`, which yields **local time**, and ticks are compared | **yes** |
| `=~` | never matches a date-looking value (it is not a string any more) | no |

The time-zone dependence is Newtonsoft's own — the same query on the same document gives the same answer in
Newtonsoft.Json on the same machine — and the tests hold the engine to it. But it does mean the *same query can answer
differently on a laptop and in a container* (containers usually run in UTC). If your documents hold dates and you did not
rely on Newtonsoft's behaviour, set `EmulateNewtonsoftDates = false`: date-looking strings are then plain strings
everywhere, and every comparison is machine-independent. (Do the same if you parsed the Newtonsoft document with
`DateParseHandling.None`.) The default stays `true` because the Newtonsoft dialect exists to give Newtonsoft's answers.
The RFC 9535 dialect never treats strings as dates.

## Limits and safety

* Every regex evaluation has a timeout (`RegexTimeout`); a pathological pattern fails with `Limit`.
* A query longer than `MaxQueryLength` is refused before it is parsed; filter nesting deeper than
  `MaxDepth` is refused rather than recursed into.
* Walking a document with `..` uses an explicit stack bounded by `MaxDepth`, not the call stack.
* Parsed queries are immutable and thread-safe; the cache is bounded.
* Numbers: integers are exact (`long`, and `BigInteger` in the Newtonsoft dialect); an integer and a
  double are compared exactly, not through a lossy conversion.

## Target frameworks

`net8.0`, `net9.0` and `net10.0`, built in parallel and each tested on its own runtime. A later runtime (`net11.0` and
beyond) uses the `net10.0` build — NuGet picks the highest compatible one — and that is checked, not assumed: the `net10.0`
build passes the compliance suite and the Newtonsoft differential corpus unchanged on the .NET 11 release candidate.

Microsoft's own support for .NET 8 and .NET 9 ends on **10 November 2026**; they are targeted because migrating teams are
still on them, and will be dropped only in a major version after that date. Policy and the runtime differences found on the
way (`JsonObject.GetAt`, `JsonNode.DeepEquals`) are in
[`docs/target-frameworks.md`](https://github.com/SQUORA-NL/TLio/blob/main/docs/target-frameworks.md).

Not targeted: `netstandard2.0` / .NET Framework 4.8 — it would need the `System.Text.Json` package as a dependency, which
ends the "no dependencies" property. Say so on the issue tracker if you need it.

## How this is verified

| What | Proof | Result |
|---|---|---|
| RFC 9535 | The official [JSONPath Compliance Test Suite](https://github.com/jsonpath-standard/jsonpath-compliance-test-suite), vendored at commit `9d1a415` (`Compliance/PINNED.md`) | 706 / 706 pass, no exceptions listed |
| Newtonsoft parity | Differential tests run every query through Newtonsoft.Json 13.0.4 and the engine and compare the nodes, their order, their normalized paths, the error *category*, and `SelectTokens` vs `SelectToken` behaviour, with and without `ErrorWhenNoMatch`: a hand-written corpus (≈1,500 queries × 13 documents), the 258 queries of [json-path-comparison](https://github.com/cburgmer/json-path-comparison)'s regression suite, every JSONPath string found in this repository's tests, docs and fixtures, and seeded generated queries (a third deliberately damaged) | 0 mismatches. A soak of 300 seeds × 1,500 queries × 4 documents (≈1.8 M comparisons, both APIs, both `ErrorWhenNoMatch` settings) is `[Explicit]`: `dotnet test TLio.JsonPath.Tests --filter Name~soak` |
| Extended | RFC queries Newtonsoft rejects get the RFC result (compliance suite); everything else gets the Newtonsoft result (oracle) | pass |
| End to end | `TLio.Json.AdapterParity.Tests` runs every fixture in the repository plus the sweep of every command and function through both `TLio.Json` and `TLio.Json.SystemText` | 328 / 328 identical documents |
| Machine independence | The suite passes under `TZ=Asia/Kolkata` and `nl_NL` | pass |

The test project depends on Newtonsoft.Json as an **oracle** only; the package does not.

## Performance

Measured with BenchmarkDotNet (`TLio.JsonPath.Benchmarks`), one run on an Apple M4 Pro / .NET 10. The full tables, method and
caveats are in [`docs/benchmarks/jsonpath-engine.md`](https://github.com/SQUORA-NL/TLio/blob/main/docs/benchmarks/jsonpath-engine.md).
They are measurements, not promises, and they include the cases where this engine is **not** the fastest:

* **Against Newtonsoft's `SelectTokens`:** faster on plain lookups (2–3×), a large filter (2×) and a large descendant
  query (1.3×); slower where a query fans out over many nodes (a 20,000-element wildcard: 2.6× the time) and on a
  four-element filter (1.4×) — Newtonsoft enumerates lazily and returns no list.
* **Against the other RFC 9535 engines:** JsonPath.Net is 2–9× slower than this engine in every case measured.
  Hyperbee.Json is faster on plain lookups (1.4–1.7×), the small descendant query and a 20,000-element wildcard, and
  slower on filters (1.5–1.9×).
* **Against the JsonCons-based strategy this package replaced** in `TLio.Json.SystemText`: faster in every case, from 2× on
  a small filter to over 100,000× for an indexed lookup in a 20,000-element document (which no longer gets serialized).

## Migrating from the JsonCons-based `TLio.Json.SystemText`

Earlier versions of `TLio.Json.SystemText` evaluated paths with JsonCons.JsonPath on a serialized
copy of the document. If you used it:

* **The default dialect is now Newtonsoft**, which is what the package always claimed to match.
  Paths that worked under Newtonsoft work here; paths that relied on RFC/JsonCons syntax —
  `[?@.a]`, `[-1]`, `match()`, double-quoted names — need `JsonPathDialect.Extended` (which accepts all of
  them and still behaves as Newtonsoft for everything Newtonsoft accepts) or `Rfc9535`.
* **Errors are no longer swallowed.** An invalid path used to select nothing silently; it now throws
  `JsonPathException`, as Newtonsoft's `SelectTokens` throws.
* **`SelectNode` on a path with several matches** used to return the first; it now throws, as
  Newtonsoft's `SelectToken` does. Use `SelectNodes(...).First`.
* JsonCons-only functions (`keys()`, `tokenize()`, `sum()`, `to_number()`, …) are gone; register an
  RFC function extension for anything you relied on.
* Selection is no longer against a snapshot: it sees the document exactly as it is, including
  mutations made earlier in the same script.

## Third-party notices

The Newtonsoft dialect is a transcription of the behaviour of **Newtonsoft.Json** 13.0.4's
`Linq/JsonPath` (`JPath`, the path filters, `QueryExpression`, `JValue.Compare`) and of the date
recognition in `JsonTextReader`/`DateTimeUtils`/`DateTimeParser`. Those files are MIT licensed:

> Copyright (c) 2007 James Newton-King
>
> Permission is hereby granted, free of charge, to any person obtaining a copy of this software and
> associated documentation files (the "Software"), to deal in the Software without restriction,
> including without limitation the rights to use, copy, modify, merge, publish, distribute,
> sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is
> furnished to do so, subject to the following conditions:
>
> The above copyright notice and this permission notice shall be included in all copies or
> substantial portions of the Software.
>
> THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING
> BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
> NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM,
> DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
> OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

## License

MIT — see the [repository](https://github.com/SQUORA-NL/TLio).
