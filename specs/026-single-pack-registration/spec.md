# Feature Specification: One registration call per pack

**Feature Branch**: `worktree-feature+fix-all-registration-packs`
**Status**: Implemented

## Origin

The NexxLogic Elsa 3 host registered Text, Math, TimeDate, ETL and Looping but never called
`RegisterFormatConversion`. `convertValue` then parsed as `NotFoundCommand` — a warning, and a
*successful* run that left the document unchanged. Whole-document `convert` still worked
(`MultiFormatScriptRunner` intercepts it), which hid the gap. The cause: a host had to know, per
pack, which provider to call on, which method name to use (Text even had two), and that format
conversion takes extra arguments.

## Decisions (agreed)

| # | Question | Decision |
|---|---|---|
| 1 | One call for everything, or per pack? | **Per pack.** Each pack registers all its commands *and* functions in one call. |
| 2 | Target type | `IRegistrationTarget<TNode, TSelf>` in `TLio.Core/Contracts`, implemented (explicitly) by `ParseOptions<TNode>`. Packs reference only `TLio.Core`, so they cannot extend `ParseOptions` (in `TLio.Client`) without a new dependency. |
| 3 | Chaining | `TSelf` is the implementing type, so every `With*` returns the concrete `ParseOptions<TNode>`: `.WithMath().WithText()…` |
| 4 | Names | `WithMath`, `WithText`, `WithTimeDate`, `WithEtl`, `WithLooping`, `WithFormatConversion(converter, documentFormatId)` |
| 5 | Existing `Register*` | Unchanged and still public. `With*` delegates to them, so each pack still has exactly one list of names. |
| 6 | Alias | `RegisterTextPack` (the only pack with an alias) is `[Obsolete]`; removed in the next major version. |
| 7 | Format conversion arguments | Required and validated; there is no overload without them. |

## Tests

`TLio.Parity.Tests/PackRegistrationTests`:

- per pack, `With*` registers exactly the command and function names the `Register*` call does
  (enumerated from the providers, not a hard-coded list), and each adds something;
- the calls chain and keep the concrete type;
- `WithFormatConversion` rejects a null converter and a null/blank format id;
- on JSON, XML and YAML engines built with `WithFormatConversion`, `convertValue` runs
  (`{"input":{"field":1}}` → `{"input":"<input><field>1</field></input>"}` on JSON).

## Follow-up (out of scope)

Unknown commands are warn-and-succeed (`NotFoundCommand`). That is what turned a missing
registration into a silent no-op. An opt-in strict mode (unknown command fails the run) would
make the same mistake loud; deliberately not changed here.
