# Release notes

What changed in each TLio release, written for someone deciding whether and how to upgrade.
Every release has its own page with an **at a glance** table (commands, functions, packages, target
frameworks), the highlights, fixes, **breaking changes and upgrade notes**, and the pull requests it
contains. The GitHub release pages carry the auto-generated changelog; these pages are the
curated version.

| Release | Date | In one line |
|---|---|---|
| [Unreleased](unreleased.md) | — | Changes on `main` since 1.4.0 |
| [1.4.0](v1.4.0.md) | 2026-10-19 | `add` appends to arrays, `copy` to the root replaces, wildcard / bracket-name / union path fixes |
| [1.3.0](v1.3.0.md) | 2026-10-19 | Schema shaping functions, MCP server fixes, cross-format examples on every page, release notes, XML comparison fix |
| [1.2.0](v1.2.0.md) | 2026-10-11 | Infix arithmetic in function values |
| [1.1.0](v1.1.0.md) | 2026-10-08 | `forEach`/`while`, own JSONPath engine, .NET 8/9/10, concurrency fixes |
| [1.0.2](v1.0.2.md) | 2026-09-25 | Faster, smaller `decisionTable` (`outputPathTemplate`) |
| [1.0.1](v1.0.1.md) | 2026-09-24 | Indexed `resolve`, dynamic `targetPath`, compiled multi-format scripts, docs sweep |
| [1.0.0](v1.0.0.md) | 2026-09-20 | `setProperties`, `toArray`, documentation audit |
| [0.9.0](v0.9.0.md) | 2026-08-23 | XML/YAML scripts, mid-script `convert`, 21 functions, tag-derived versions, strong naming |
| [0.8.0](v0.8.0.md) | 2026-08-20 | Null handled the same in every format, XPath-correct XML paths, `rename`, structural `compare` |

## Growth at a glance

| Release | Documented commands | Documented functions | NuGet packages | Target frameworks |
|---|---|---|---|---|
| 0.8.0 | 15 | 68 | 12 | net10.0 |
| 0.9.0 | 17 | 89 | 13 | net10.0 |
| 1.0.0 – 1.0.2 | 18 | 98 | 13 | net10.0 |
| 1.1.0 – 1.2.0 | 20 | 99 | 15 | net8.0, net9.0, net10.0 |
| 1.3.0 – main | 20 | 102 | 15 | net8.0, net9.0, net10.0 |

Counts are the pages under `docs/ai-ref/commands` and `docs/ai-ref/functions` and the projects that
opt into `IsPackable`, read from each tag.

## Breaking changes, all in one place

| Release | Change | What to do |
|---|---|---|
| 1.4.0 | `add` on an existing array appends instead of skipping, so re-running a script appends again | Use `put` or `set` where the array should be set, not extended |
| 1.4.0 | `copy` to `$` replaces the root with the copied content instead of merging into it | Use `merge` where the existing root properties must survive |
| 1.3.0 | In XML, two values now compare equal when their content is equal even if the elements are named differently (as in JSON and YAML) | A `resolve` or comparison that matched nothing in XML only because key names differed will now match |
| 1.1.0 | `TLio.Json.SystemText` uses the Newtonsoft path dialect by default; invalid paths throw; `SelectNode` with several matches throws | Opt in to `Rfc9535`/`Extended` if you relied on JsonCons behaviour; handle `JsonPathException` |
| 1.1.0 | Samples, `docs/samples`, `docs/showcase`, `demo/` left this repository | Use [TLio-Samples](https://github.com/SQUORA-NL/TLio-Samples) |
| 0.9.0 | Assemblies are strong-named | Recompile anything built against an unsigned TLio |
| 0.8.0 | XML paths anchor on the document node; the document element must be named | Prefix XML paths with it (`/order/customer`) |

## How releases are cut

The git tag is the version (see [versioning](../versioning.md)). To cut one, run the **Release**
workflow or push a `vX.Y.Z` tag. When you do:

1. Rename `unreleased.md` to `vX.Y.Z.md`, fill in the *at a glance* table, add the release to the
   table above, and start a fresh `unreleased.md`.
2. Keep the GitHub release body short and link to the page here.

While working, add a line to `unreleased.md` for anything a user would notice: a new command or
function, a changed default, a fix with a visible symptom, and always an **upgrade note** for a
breaking change.
