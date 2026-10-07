# ADR 0001 — A TLio-owned JSONPath engine, shipped as its own package

Status: accepted

## Context

`TLio.Json.SystemText` promised "identical transformation behaviour" to `TLio.Json`, but its path
language came from JsonCons.JsonPath while `TLio.Json` uses Newtonsoft.Json's `SelectToken(s)`. Those
are different languages:

* Newtonsoft's JSONPath predates RFC 9535 and is defined by its implementation (no grammar): it
  accepts `$.a-b`, requires `[?( … )]`, walks `JProperty` wrappers when a filter runs over an object,
  includes the root in `$..*`, drops the `..` in `$..[0]`, nests `a && b || c` to the right, converts
  strings to numbers in `<`, treats date-looking strings as dates, and throws on `[-1]`.
* JsonCons implements its own dialect (the json-path-comparison consensus plus its own grammar);
  its last release was 1.1.0 in August 2021.
* RFC 9535 is the standard and differs from both.

So the same script gave different results depending on the adapter. JsonCons also forced a design
that serialized the whole `JsonNode` tree to a string, parsed it, selected on the copy and then
navigated back to the live nodes — on every selection.

## Decision

1. **Write the engine in this repository** and ship it as a separate package, `TLio.JsonPath`,
   with no dependency on TLio, on Newtonsoft.Json or on anything but the BCL and System.Text.Json. It
   runs directly on `JsonNode`, so selected nodes are the live nodes.
2. **Make the dialect an explicit option** (`Newtonsoft` — the default, `Rfc9535`, `Extended`), part of
   the engine's options, never a per-call or global switch.
3. **Establish Newtonsoft behaviour empirically.** Newtonsoft.Json 13.0.4 is the oracle: the Newtonsoft
   dialect is a transcription of its `JPath` parser, path filters, query expressions and `JValue`
   comparison (MIT-licensed, attributed), kept deliberately un-tidied, and every claim about it is a
   differential test against the real library.

## Why not an existing library

JsonPath.Net (json-everything) and Hyperbee.Json both implement RFC 9535 over System.Text.Json, and
either could have served the RFC dialect. Neither can serve the requirement that drives this decision:
reproducing *Newtonsoft's* dialect — a moving target of accidents (filters over `JProperty`, right-nested
boolean chains, string↔number coercion, date recognition) that only a transcription pinned by a
differential suite can guarantee. Adopting one for RFC and writing the Newtonsoft dialect ourselves
would have meant two engines, two sets of node/null/number semantics and two answers to "what is a
match", whereas a single AST, node-model abstraction and result type let the dialects share an
evaluator's plumbing and differ only where they must. A third-party dependency would also have
contradicted the point of the package: a System.Text.Json adapter with no third-party dependencies.
JsonCons was rejected on the evidence above (a third dialect, unmaintained since 2021, and a design that
cannot hand back live nodes without a round trip).

## The dialect model

* `Newtonsoft` — default; bit-for-bit Newtonsoft.Json 13.0.4, so existing scripts keep working.
* `Rfc9535` — the whole standard: grammar, selectors, filters with deep-equality and Nothing, function
  extensions with parse-time typing, I-Regexp (RFC 9485), normalized paths. One documented
  deviation (`^`/`$` anchors), switchable with `StrictIRegexp`.
* `Extended` — Newtonsoft first; text Newtonsoft rejects is read as RFC 9535; plus RFC's negative index,
  where Newtonsoft has no answer (it crashes). A strict superset of `Newtonsoft` wherever Newtonsoft produces a
  result.

Two grammar front-ends (the RFC parser, and the Newtonsoft transcription) feed two evaluators over one
document-model interface (`IJsonModel<TNode>`, implemented for `JsonNode`; nothing outside it names
System.Text.Json.Nodes). A JToken model could be added later so that both TLio adapters run on this engine.

## Parity strategy

* Differential tests (values, order, normalized paths, error category, `SelectTokens` vs `SelectToken`,
  `ErrorWhenNoMatch`) over a hand-written corpus, the json-path-comparison regression suite, every path
  used in this repository and seeded generated queries, plus an explicit long soak.
* The official compliance suite, pinned, for RFC.
* End to end: every repository fixture and the sweep of every command and function run through both
  adapters with identical output.
* Deliberate divergences are executable rows in `DivergenceTableTests` — they can neither grow nor be
  fixed silently.

## Consequences

* Behavioural changes for `TLio.Json.SystemText` users (documented as a migration note): Newtonsoft is
  now the default dialect; invalid paths throw instead of selecting nothing; `SelectNode` on several
  matches throws like `SelectToken`.
* The engine honours Newtonsoft's quirks, including time-zone-dependent date handling, and says so.
* We own its maintenance, including following Newtonsoft if it ever changes its JSONPath. The oracle
  tests will say so.
* Reported: the engine is faster than the replaced strategy in every benchmark case
  (`docs/benchmarks/jsonpath-engine.md`).
