# Change history and design notes

Moved out of `CLAUDE.md`, which keeps only what an editor needs on every task.
Newest first.

- split-samples: samples, `docs/samples`, `docs/showcase` and `demo/` moved to the TLio-Samples
  repository and now build against the TLio NuGet packages. Gone from here with them: the
  solution's sample projects, `global.json` / `Directory.Solution.targets` (Azure Functions SDK,
  AzureDemo only), `.dockerignore`, CI's Docker image job, and the `docs/samples` round trip in
  `TLio.Parity.Tests/ScriptSerializationTests` (now `tests/TLio.Samples.Tests` there).
- feature/actus-pam-contract: `TLio.Extensions.Looping` — `forEach`/`while`, the iteration
  primitive TLio previously had no equivalent of (`TLioScript<TNode>` is a strictly linear
  list; `decisionTable`/`resolve` loop internally but don't expose iteration to a script
  author). Both use the existing nested-`TLioScript` pattern `ifElse` already established. The
  current element is `IExecutionContext.CurrentNode` (new — the one core addition this needed:
  a settable `TNode? CurrentNode` on `IExecutionContext<TNode>`, null outside any loop, fully
  additive), which `forEach` sets around each iteration (nested loops nest for free via the C#
  call stack) and which `PropertyChangeCommand` (`set`/`add`/`put`) now consults: a `path`
  beginning with `@` (or bare `@` for the whole element) resolves against it before the usual
  array-index/selector classification runs, and every `Value.GetValue(...)` call site prefers
  `context.CurrentNode` over its own resolved target — so `@`/`@.field` works both as a path and
  inside a value, even when the value is read while writing somewhere else entirely (a running
  total elsewhere in the document). `ReplaceKeepingCurrentNodeInSync` (in `PropertyChangeCommand`)
  keeps `CurrentNode` from going stale after `set path="@" value=...`: `Replace` detaches the old
  node rather than mutating it, so without this a second `@` read later in the same iteration
  would see the pre-replace value.

  **Bug found and fixed after the initial merge:** the first cut of `@` resolution had
  `PropertyChangeCommand.Execute` temporarily overwrite its own `Path` property with the
  resolved absolute path, then restore it in a `finally`. `Path` looked like ordinary per-call
  state, but the command instance is a node in the *compiled* script tree — a singleton shared
  across every `forEach` iteration *and* every concurrent execution of that compiled script
  (e.g. two overlapping HTTP requests against one long-lived host, exactly `TLio.Sample.Actus.Api`'s
  shape: one `CompiledScript<JToken>` built once at startup, `Execute`d per request). Before `@`
  needed resolving, `Path` was immutable during `Execute`, so sharing it was safe; the moment it
  became write-then-restore, two concurrent requests hitting the same `set path="@"` command
  raced on it — one request's resolved index could get clobbered by another's before it was
  read, corrupting array writes and reads under load (`$.schedule[1]` silently unwritten while
  `$.schedule[2]` received someone else's value; a `@` read returning "$", the root indicator,
  because the resolved path had been reset out from under it). Reproduced by firing concurrent
  requests at the running sample API; never reproduced single-threaded, which is why the initial
  single-request testing missed it. Fixed by never writing back to `Path`: `Execute` resolves
  into a local and threads it as a parameter through `ExecuteWithResolvedPath` /
  `ExecuteNewSyntax` / `ExecuteLegacySyntax` / `ApplyValueToMissingIndex` / `WarnNoIndex`, all of
  which read the parameter instead of `this.Path`. `Path` itself is now genuinely read-only for
  the lifetime of the compiled command. General lesson for any future command: a compiled
  script's commands are shared, concurrently-executed singletons — request-scoped state belongs
  on `IExecutionContext<TNode>` (already true of `CurrentNode`), never on a mutable property of
  the command itself, even "temporarily."

  An early, broader version of this went through `IItemsFetcher.IsPathExpression` (relaxing it
  to accept a *bare* `@`/`$`, so `=fetch(@)` could read "the whole current item" as a value) and
  had to be reverted: that method also gates `ResolveArg`/`Fetch`'s *runtime* re-check of an
  already-resolved value, with no way to tell a script-typed path from a quoted literal that
  happens to match — XML's current-item token is `.`, an ordinary character in real data, and
  the sweep caught `padLeft(...,'.')` being reinterpreted as "the current node." Net effect:
  `@`/`.` bare work as a command's own `path` (goes through `ResolveRelativePath` directly, never
  touches `IsPathExpression`), but not as a bare value — read a field (`@.field`) instead, or for
  a scalar element use `=fetch(=scriptpath())` (`scriptpath()` bare returns the current element's
  own path as a *computed* string, not typed text, so re-resolving it is unambiguous).

  No `appendTo` — `add` only ever appends at a literal "next free" index, which a loop body can't
  compute for itself, and an earlier version that special-cased this in `forEach`/`while` was
  rejected as too restrictive. The idiom instead: `forEach` transforms elements in place via
  `set path="@"`; growing a list during a loop builds a delimited string (`concat`) and
  `split()`s it once afterward — ordinary script, nothing loop-specific.

  Demonstrated end-to-end by `samples/TLio.Sample.Actus.Api`, an ACTUS PAM (Principal at
  Maturity) contract calculator: `while` walks the interest-payment cycle to build the schedule
  (mirroring the ACTUS reference implementation's own schedule loop), `forEach` folds each
  date's day-count fraction and payoff against running state. Also added: `dayCountFraction`
  (A360/A365/30E360) to `TLio.Extensions.TimeDate`.

  Two XML-adapter gotchas surfaced along the way, worth knowing for any future command that
  creates/attaches a fresh node then keeps using the local reference, or that iterates a
  multi-element array: `XmlNodeAdapter.SetProperty`'s `Rename` returns a *new* `XElement`
  whenever the source's name or attachment state doesn't already match (re-fetch via
  `GetProperty` after attaching, don't keep the pre-attach reference), and
  `SlashPathItemsFetcher.GetPath` does not disambiguate same-named siblings (returns
  `/root/tags/item` for either element of a two-item array, so a bare `@`/`.` `path` resolving
  through it is only reliable for a single-element array) — both pre-existing limitations, the
  second not fixed by this change (the sweep works around it, see `Sweep/sweep.xml`).
- setProperties command + scriptpath find mode: `setProperties` runs any value function against
  a *selection* of nodes under one or more matched objects — the only way to write through an
  object-key wildcard (`$.obj.*`) or a multi-key union (`$.obj['a','b']`), since `set`/`add`/`put`
  only resolve a leaf as a multi-node selector when it is bracket-and-subscript shaped, and
  neither of those forms is. `properties` takes either a literal array of names/`@.`-relative
  paths, or a function — typically the new `=scriptpath(*, kinds, recursive)` shape, which finds
  descendant nodes by kind (`object`/`primitive`/`null`) and returns them as live nodes (not a
  document array) for direct write-back. Also fixed: function-call arguments can now contain
  array literals (`=fn(['a','b'])`) — `FunctionConverter` previously read `[...]` as literal text
  and split on any internal comma, which silently broke a multi-element array argument.
- toArray function: `=toArray()` / `=toArray(path)` — the array sibling of `promote`. Wraps a
  node in a fresh array (current value, if any, as the sole element); `[]` when the source is
  absent or `null`; an already-array source passes through as a deep clone, never double-wrapped.
  With no argument it wraps the *current node*, the same "current context" `scriptpath()` falls
  back to bare, so it wraps each match individually over a wildcard path.
- 024-package-format-converter: format conversion ships on NuGet as `TLio.FormatConverter` —
  the five projects merged into one assembly at the repo root, named like every other project
  (package = assembly = namespace root = folder). Namespaces moved from `FormatConverter.*`
  to `TLio.FormatConverter.*`; `FormatConverter.TLio` is now `TLio.FormatConverter`.
- 023-function-gaps: 21 functions added so one idea stops costing four levels of nesting —
  Math `multiply` / `divide` / `clamp` / `sign`; TimeDate `dateDiff` / `dateAdd` / `datePart` /
  `formatDate` / `parseDate` / `startOfMonth` / `endOfMonth`; Text `regexReplace` /
  `regexExtract` / `right`; built-in `if` / `coalesce` / `between` / `distinct` / `sort` /
  `sortBy` / `last`. The two car-insurance samples were rewritten onto them with byte-identical
  output. Analysis and rationale: `docs/function-gaps.md`.
- 022-format-convert-command: `convert` usable mid-script — section executors, canonical shape,
  `convertValue` for in-place values, `textProperty`/`namespacePrefix` honoured, YAML emitted
  through YamlDotNet, FormatConverter folded into `TLio.sln`.
- 021-script-notation-parsers: XML and YAML script notations reachable from `ScriptEngine`,
  MCP (`tlio_execute` gained `scriptFormat`) and both samples; notation detection; structured
  values in XML/YAML now typed and function-expanding like JSON's; parse failures carry a
  reason instead of an empty script.
- 020-xml-alignment: XML and YAML brought onto the JSON data model — canonical document shape,
  array/object split, script-notation parity (bools, enums, nested scripts, settings), YAML
  recursive descent, format-aware path detection in functions. Added `TLio.Parity.Tests`.
- 019-mcp-tlio-server: Added C# / .NET 10 + `ModelContextProtocol` (Anthropic MCP SDK, stdio server), `System.Threading.RateLimiting` (in-box .NET), `TLio.Json`, `TLio.Json.SystemText`, `TLio.Xml`, `TLio.Yaml`, `TLio.Client`, `TLio.Commands`, `TLio.Functions`, `TLio.Extensions.*`
- 018-api-script-slug-cache: Added C# / .NET 10 + ASP.NET Core Minimal API; `TLio.Client` (`ScriptEngine<TNode>`, `CompiledScript<TNode>`); `TLio.Json` (`JsonExecutionContext`, `JsonNodeAdapter`); `TLio.Xml` (`XmlExecutionContext`); `TLio.Yaml` (`YamlExecutionContext`); NuPlane + CShells (DockerPlugin host only)
- 017-im-format-converter: Added C# / .NET 10 + `System.Text.Json` (built-in), `System.Xml` (built-in), `YamlDotNet` (MIT, YAML adapter only), `NUnit 4.x` (tests), `TLio.Core` (FormatConverter.TLio only)


<!-- MANUAL ADDITIONS START -->
<!-- MANUAL ADDITIONS END -->
