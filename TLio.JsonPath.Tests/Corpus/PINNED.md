# Query corpora

| File | What it is | Source |
|---|---|---|
| `jpc-regression.json` | The 258 queries (selector + document) of the cburgmer/json-path-comparison regression suite, converted from `regression_suite/regression_suite.yaml` (the consensus results are dropped: the oracle is Newtonsoft.Json 13.0.4 itself, not a consensus) | <https://github.com/cburgmer/json-path-comparison> at commit `c9814eee8a148645023069d538d055960cf81456` |
| `tlio-paths.txt` | Every distinct string beginning with `$` found in this repository's tests, fixtures, docs and samples (1,100+; deliberately includes noise, which is useful for error parity) | extracted from the repository |

Neither file asserts expected values. `NewtonsoftDifferentialTests` runs each query through Newtonsoft and
through the engine and requires the two to agree.
