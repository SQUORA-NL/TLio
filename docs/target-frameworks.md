# Target frameworks

## What is built

| | Targets |
|---|---|
| Every library package (`TLio.Core`, `TLio.Commands`, `TLio.Functions`, `TLio.Client`, the adapters, the extension packs, `TLio.FormatConverter`, `TLio.JsonPath`) | `net8.0`, `net9.0`, `net10.0` |
| Every test project | the same three, so each is exercised on its own runtime |
| Applications (`TLio.Mcp`, the benchmark consoles) and the tests of an application (`TLio.Mcp.Tests`) | `net10.0` only |

The list lives in one place, `Directory.Build.props` (`TargetFrameworks`). An application opts out with its own
`<TargetFrameworks>`.

## Which runtimes are still supported by Microsoft

From <https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core> (checked October 2026):

| .NET | Type | End of support |
|---|---|---|
| 10 | LTS | 14 Nov 2028 |
| 9 | STS (24 months) | **10 Nov 2026** |
| 8 | LTS | **10 Nov 2026** |
| 11 | STS (in RC at the time of writing) | — |

`net8.0` and `net9.0` are kept because a large part of the Newtonsoft → System.Text.Json migration crowd is still on them,
not because they will stay supported: both lose Microsoft support on the same day.

## Rules

1. **New runtimes are added when they ship.** When .NET N reaches GA, add `netN.0` to `Directory.Build.props`, run the
   whole suite on it, and extend the `setup-dotnet` versions in `.github/workflows/ci.yml`. Until then, the
   `forward-compat` CI job runs the newest `net10.0` test build on the newest preview runtime and fails (informationally)
   if it breaks or if it did not really run there.
2. **Old runtimes are dropped only in a major version**, and only after Microsoft's end of support. `net8.0` and `net9.0`
   can go in the first major release after 10 Nov 2026; removing a target is a breaking change for anyone still on it.
3. **A package built for `netX.0` runs on every later runtime.** NuGet picks the highest compatible `lib/` folder, so an
   application on `net11.0` or later receives the `net10.0` build until a `net11.0` one exists. This is checked, not
   assumed: the `net10.0` build was run on the .NET 11 release candidate (compliance suite 706/706, 0 mismatches against
   Newtonsoft over the differential corpus).
4. **Conditional code uses `NETx_0_OR_GREATER`, never `NETx_0`.** `#if NET9_0_OR_GREATER` stays true on every later target;
   `#if NET9_0` would silently fall back to the slow path on `net10.0` and beyond.
5. **Runtime differences are bugs in our code until proven otherwise.** Two turned up when `net8.0` was added, and are the
   reason rule 6 exists:
   * `JsonObject.GetAt` (reading an object's N-th member) only exists from .NET 9. On `net8.0` `TLio.JsonPath` reads members
     through a sequential cursor, dropped at the end of every selection so it cannot go stale.
   * `JsonNode.DeepEquals` compares `42` and `42.0` as *different* on .NET 8 and as equal from .NET 9. The
     System.Text.Json adapter's `DeepEquals` (used by `compare` and `merge`) is now an explicit, numeric-aware
     implementation, so commands answer the same on every runtime.
6. **Every target is tested on its own runtime**, in CI and locally (below). A target that is only compiled is not supported.

## Not targeted

`netstandard2.0` / .NET Framework 4.8. Reaching them would mean taking the `System.Text.Json` NuGet package as a dependency
(`TLio.JsonPath` today has none), polyfilling newer language and BCL features, and carrying a fourth test configuration.
It is feasible; it is a separate decision from supporting the current .NET releases. Say so on the issue tracker if you need it.

## Checking it yourself

```sh
dotnet build -warnaserror
dotnet test                       # runs every test project on every target (needs the 8, 9 and 10 runtimes)
dotnet test -f net8.0             # one runtime
```

Each run of `TLio.JsonPath.Tests` prints the runtime it ran on (`RuntimeReportTests`), so the log shows what was exercised.

To check a runtime that has no target yet (a preview), run the existing build on it:
install the runtime next to the SDK and run the tests with `DOTNET_ROLLFORWARD=LatestMajor` and
`DOTNET_ROLL_FORWARD_TO_PRERELEASE=1` — which is what the `forward-compat` job does.
