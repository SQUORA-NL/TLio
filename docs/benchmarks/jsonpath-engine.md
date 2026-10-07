# TLio.JsonPath benchmarks

Measured with BenchmarkDotNet 0.14.0 (`TLio.JsonPath.Benchmarks`, `PathBenchmarks`; 3 warm-up + 12 measured iterations per case) on an Apple M4 Pro, macOS, .NET 10.0.10, Release build.
**One run on one machine.** These are measurements, not guarantees; re-run on your hardware with
`dotnet run -c Release --project TLio.JsonPath.Benchmarks -- --filter '*'`.

The same logical query is run four ways, against a document that does not change between calls:

* **Newtonsoft** — `JToken.SelectTokens` on a `JToken` (reference). It enumerates lazily and returns no list, so it allocates least.
* **OldJsonCons** — the strategy `TLio.Json.SystemText` used before: serialize the `JsonNode` tree, reuse the previous `JsonDocument` parse when the text is unchanged, select with JsonCons, navigate the live tree by each normalized path. This is that design's *best case* (one fetcher, selector cached, document unchanged).
* **EngineNewtonsoft** — `TLio.JsonPath`, Newtonsoft dialect on a `JsonNode` (the `TLio.Json.SystemText` default). Returns a materialised list of matches.
* **EngineRfc9535** — `TLio.JsonPath`, RFC 9535 dialect, same query in RFC syntax.

`small` is the usual bookstore document; `large` is an array of 20,000 objects (each with a nested object and an array). Each setup first checks that all four return the same number of nodes.

## Mean time per call (allocated per call)

| Document | Query | Newtonsoft | OldJsonCons | EngineNewtonsoft | EngineRfc9535 |
|---|---|---|---|---|---|
| small | simple | 134.2 ns (632 B) | 816.9 ns (1.8 KB) | 55.1 ns (336 B) | 63.1 ns (424 B) |
| small | indexed | 177.4 ns (760 B) | 819.4 ns (1.9 KB) | 53.5 ns (336 B) | 65.7 ns (424 B) |
| small | wildcard | 213.9 ns (784 B) | 1.30 µs (3.1 KB) | 281.7 ns (1.0 KB) | 185.5 ns (1.1 KB) |
| small | filter | 468.6 ns (2.0 KB) | 1.49 µs (3.3 KB) | 653.1 ns (1.3 KB) | 437.9 ns (1.3 KB) |
| small | descendant | 566.1 ns (328 B) | 1.99 µs (6.6 KB) | 522.4 ns (1.3 KB) | 532.2 ns (2.4 KB) |
| large | simple | 126.1 ns (616 B) | 5.16 ms (3.9 MB) | 46.9 ns (336 B) | 58.6 ns (424 B) |
| large | indexed | 91.4 ns (480 B) | 5.14 ms (3.9 MB) | 39.7 ns (336 B) | 51.8 ns (424 B) |
| large | wildcard | 1.00 ms (634 B) | 16.53 ms (12.9 MB) | 1.87 ms (2.5 MB) | 3.48 ms (8.5 MB) |
| large | filter | 4.21 ms (4.1 MB) | 10.07 ms (9.9 MB) | 2.32 ms (1.2 MB) | 2.16 ms (2.2 MB) |
| large | descendant | 4.19 ms (334 B) | 22.84 ms (34.8 MB) | 3.10 ms (2.5 MB) | 4.47 ms (7.0 MB) |

## Against the implementation it replaces

| Document | Query | EngineNewtonsoft vs OldJsonCons | EngineRfc9535 vs OldJsonCons |
|---|---|---|---|
| small | simple | 14.8× faster | 12.9× faster |
| small | indexed | 15.3× faster | 12.5× faster |
| small | wildcard | 4.6× faster | 7.0× faster |
| small | filter | 2.3× faster | 3.4× faster |
| small | descendant | 3.8× faster | 3.7× faster |
| large | simple | 110,108.0× faster | 88,045.0× faster |
| large | indexed | 129,669.1× faster | 99,342.9× faster |
| large | wildcard | 8.8× faster | 4.7× faster |
| large | filter | 4.3× faster | 4.7× faster |
| large | descendant | 7.4× faster | 5.1× faster |

Every case is faster than the replaced implementation (smallest margin 2.3×); on the large document an indexed or simple lookup no longer pays to serialize 20,000 objects.

## Against Newtonsoft itself

| Document | Query | EngineNewtonsoft vs Newtonsoft |
|---|---|---|
| small | simple | 2.44× faster |
| small | indexed | 3.31× faster |
| small | wildcard | 1.32× the time |
| small | filter | 1.39× the time |
| small | descendant | 1.08× faster |
| large | simple | 2.69× faster |
| large | indexed | 2.30× faster |
| large | wildcard | 1.87× the time |
| large | filter | 1.81× faster |
| large | descendant | 1.35× faster |

The Newtonsoft dialect is slower than Newtonsoft's own lazily-enumerating `SelectTokens` where a query fans out over many nodes
(`large/wildcard`) and on the smallest filter: it materialises a list of `JsonPathMatch` structs and keeps Newtonsoft's lazy-iterator evaluation order so that errors surface identically.
It is faster on plain lookups and on the large filter and descendant queries.

## What changed to get here

A first measurement (recorded in the commit history) had the Newtonsoft dialect allocating a location object per visited node and an iterator per container. Matches now record only their last step (container, name or index) and resolve `NormalizedPath` lazily with one shared walk; descendant scans use an explicit frame stack and never create the `JProperty` wrappers they skip; filter comparisons between literals and plain paths, and plain `$.a.b[0]` queries, are followed directly instead of through the iterator chain. The differential suite was re-run after each change.
