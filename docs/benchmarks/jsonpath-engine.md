# TLio.JsonPath benchmarks

Measured with BenchmarkDotNet 0.14.0 (`TLio.JsonPath.Benchmarks`, `PathBenchmarks`; 3 warm-up + 12 measured iterations per case) on an Apple M4 Pro, macOS, .NET 10.0.10, Release build.
**One run on one machine.** These are measurements, not guarantees; re-run on your hardware with
`dotnet run -c Release --project TLio.JsonPath.Benchmarks -- --filter '*'`.

The same logical query is run through six implementations, on a document that does not change between calls. Before timing, each setup checks that all six return the same number of nodes (a disagreement aborts the benchmark).

| Implementation | What it is |
|---|---|
| Newtonsoft `SelectTokens` | `JToken.SelectTokens`, the reference. Enumerates lazily and returns no list, so it allocates least on wide results |
| TLio.JsonPath (Newtonsoft dialect) | the `TLio.Json.SystemText` default; materialises a list of matches |
| TLio.JsonPath (RFC 9535) | same engine, RFC dialect, query in RFC syntax |
| JsonPath.Net 3.0.2 | json-everything's RFC 9535 engine; query parsed once, `Evaluate(JsonNode)`; its result carries each match's location, which this engine defers until asked |
| Hyperbee.Json 3.3.2 | RFC 9535, `JsonNode.Select(path)`, enumerated |
| JsonCons strategy | what `TLio.Json.SystemText` did before: serialize the tree, reuse the previous parse if unchanged, select, navigate back to live nodes (that design's best case) |

`small` is the usual bookstore document; `large` is an array of 20,000 objects (each with a nested object and an array).

## Mean time per call (allocated per call)

| Document | Query | Newtonsoft `SelectTokens` | TLio.JsonPath (Newtonsoft dialect) | TLio.JsonPath (RFC 9535) | JsonPath.Net | Hyperbee.Json | JsonCons strategy (replaced) |
|---|---|---|---|---|---|---|---|
| small | simple | 140 ns (632 B) | 59 ns (336 B) | 64 ns (424 B) | 476 ns (2.2 KB) | 40 ns (72 B) | 807 ns (1.8 KB) |
| small | indexed | 183 ns (760 B) | 55 ns (336 B) | 68 ns (424 B) | 637 ns (2.9 KB) | 48 ns (72 B) | 833 ns (1.9 KB) |
| small | wildcard | 212 ns (784 B) | 287 ns (1.0 KB) | 192 ns (1.1 KB) | 1.33 µs (5.5 KB) | 289 ns (864 B) | 1.41 µs (3.1 KB) |
| small | filter | 475 ns (2.0 KB) | 649 ns (1.3 KB) | 443 ns (1.3 KB) | 2.33 µs (9.8 KB) | 651 ns (2.0 KB) | 1.50 µs (3.3 KB) |
| small | descendant | 610 ns (328 B) | 532 ns (1.3 KB) | 532 ns (2.4 KB) | 4.50 µs (20.3 KB) | 442 ns (2.3 KB) | 2.04 µs (6.6 KB) |
| large | simple | 129 ns (616 B) | 47 ns (336 B) | 59 ns (424 B) | 486 ns (2.2 KB) | 38 ns (72 B) | 5.25 ms (3.9 MB) |
| large | indexed | 93 ns (480 B) | 40 ns (336 B) | 53 ns (424 B) | 304 ns (1.5 KB) | 31 ns (72 B) | 5.38 ms (3.9 MB) |
| large | wildcard | 1.08 ms (632 B) | 2.79 ms (2.5 MB) | 3.89 ms (8.5 MB) | 6.88 ms (26.9 MB) | 2.98 ms (2.9 MB) | 17.12 ms (12.9 MB) |
| large | filter | 5.05 ms (4.1 MB) | 2.51 ms (1.2 MB) | 2.49 ms (2.2 MB) | 8.00 ms (33.0 MB) | 4.64 ms (8.0 MB) | 10.51 ms (9.9 MB) |
| large | descendant | 3.93 ms (334 B) | 3.08 ms (2.5 MB) | 4.27 ms (7.0 MB) | 32.53 ms (134.4 MB) | 4.23 ms (14.9 MB) | 24.85 ms (34.8 MB) |

## The other RFC 9535 engines, relative to TLio.JsonPath (RFC 9535)

| Document | Query | JsonPath.Net | Hyperbee.Json |
|---|---|---|---|
| small | simple | 7.5× the time | 1.6× faster |
| small | indexed | 9.3× the time | 1.4× faster |
| small | wildcard | 6.9× the time | 1.5× the time |
| small | filter | 5.3× the time | 1.5× the time |
| small | descendant | 8.4× the time | 1.2× faster |
| large | simple | 8.2× the time | 1.5× faster |
| large | indexed | 5.8× the time | 1.7× faster |
| large | wildcard | 1.8× the time | 1.3× faster |
| large | filter | 3.2× the time | 1.9× the time |
| large | descendant | 7.6× the time | ≈ equal |

Read each cell as the *other* engine's time relative to TLio.JsonPath's: “5.0× the time” means the other engine is five times slower; “1.5× faster” means it is faster than TLio.JsonPath.

* **JsonPath.Net** is the slowest RFC engine in every case here, and allocates the most. Part of that is by design — it builds each match's location up front, which this engine computes only if you ask for `NormalizedPath` — so it does more work per call; the gap is still large.
* **Hyperbee.Json** is faster than this engine on plain lookups (`$.a.b[0]`, with almost no allocation), on the small descendant query and on a 20,000-element wildcard; this engine is faster on both filters and on the small wildcard, and about equal on the large descendant query.


## TLio.JsonPath (Newtonsoft dialect), relative to Newtonsoft's `SelectTokens`

| Document | Query | TLio.JsonPath (Newtonsoft dialect) |
|---|---|---|
| small | simple | 2.4× faster |
| small | indexed | 3.3× faster |
| small | wildcard | 1.3× the time |
| small | filter | 1.4× the time |
| small | descendant | 1.1× faster |
| large | simple | 2.7× faster |
| large | indexed | 2.3× faster |
| large | wildcard | 2.6× the time |
| large | filter | 2.0× faster |
| large | descendant | 1.3× faster |

Slower than Newtonsoft's lazily-enumerating `SelectTokens` where a query fans out over many nodes (`large/wildcard`) and on the smallest filter: this engine materialises a list of `JsonPathMatch` structs and keeps Newtonsoft's lazy-iterator evaluation order so errors surface identically.

## The strategy this engine replaced, relative to the new engine

| Document | Query | JsonCons strategy (replaced) |
|---|---|---|
| small | simple | 13.8× the time |
| small | indexed | 15.2× the time |
| small | wildcard | 4.9× the time |
| small | filter | 2.3× the time |
| small | descendant | 3.8× the time |
| large | simple | 111,047.7× the time |
| large | indexed | 134,090.4× the time |
| large | wildcard | 6.1× the time |
| large | filter | 4.2× the time |
| large | descendant | 8.1× the time |

Every case is faster than the replaced strategy; on the large document an indexed or simple lookup no longer serializes 20,000 objects.

## What changed to get here

A first measurement had the Newtonsoft dialect allocating a location object per visited node and an iterator per container. Matches now record only their last step (container, name or index) and resolve `NormalizedPath` lazily with one shared walk; descendant scans use an explicit frame stack and never create the `JProperty` wrappers they skip; filter comparisons between literals and plain paths, and plain `$.a.b[0]` queries, are followed directly instead of through the iterator chain. The differential suite was re-run after each change.

## Caveats

* One machine, one run, a document that never changes between calls. Cold-start, parse-time and multi-threaded behaviour are not measured.
* The comparison is of selecting nodes. The engines differ in what else they give you (this one: Newtonsoft-identical results, errors by category, limits and timeouts, lazy normalized paths).
* JsonPath.Net and Hyperbee.Json are used through their documented entry points with default settings; a differently tuned use may do better.
