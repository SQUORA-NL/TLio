# Unreleased

Changes on `main` since [1.2.0](v1.2.0.md). The next tag turns this page into that release's notes
(see [How releases are cut](README.md#how-releases-are-cut)).

## At a glance

| | 1.2.0 | main |
|---|---|---|
| Documented commands | 20 | 20 |
| Documented functions | 99 | 102 (adds `orderBySchema`, `filterBySchema`, `skeleton`) |
| NuGet packages | 15 | 15 |
| Target frameworks | net8.0, net9.0, net10.0 | net8.0, net9.0, net10.0 |

## Highlights

### Schema shaping: `orderBySchema`, `filterBySchema`, `skeleton`
Built-in functions that read the *shape* of a JSON Schema — property order, declared properties,
items, local `$ref`, `allOf`/`oneOf`/`anyOf` — with no schema library. They are not a validator.
The schema is a path to a schema node (in a JSON, XML or YAML document) or JSON text; the data is an
explicit argument and the output is a fresh clone.

- `orderBySchema` puts every object's properties in the schema's order.
- `filterBySchema` keeps only the properties the schema describes.
- `skeleton` builds the empty document the schema describes: declared properties in order, empty
  arrays and defaults.
- Opt-in `'discriminate'` picks the one `oneOf`/`anyOf` branch whose `const`/`enum` tag matches the
  data, and falls back to the union of branches whenever it cannot decide.

Decisions: [behaviour-decisions](../behaviour-decisions.md) section C. Pages have JSON, XML and YAML
examples. ([#74](https://github.com/SQUORA-NL/TLio/pull/74), [#75](https://github.com/SQUORA-NL/TLio/pull/75))

### MCP server
- `tlio_execute` rejected `forEach` and `while` because the server never registered the Looping
  pack; it does now.
- The published server bundles `docs/ai-ref`, so it finds its documentation from any directory.
- `tlio_describe` reported camelCase commands (`forEach`, `ifElse`, …) as functions; the type now
  comes from the documentation folder.
- New [`TLio.Mcp/README.md`](../../TLio.Mcp/README.md): build, register with Claude Code, Claude
  Desktop and VS Code, configuration variables, troubleshooting.

([#73](https://github.com/SQUORA-NL/TLio/pull/73))

### Documentation: every command and function in JSON, XML and YAML
Each of the 122 command and function pages has an *Examples in every format* section with
collapsible JSON, XML and YAML tabs showing the input, the script and the result. They are
generated from the test suite — read from existing fixtures or from
`TLio.Parity.Tests/DocExamples/`, run through the engine as written in each notation, and checked
against an expected result — and `dotnet test` fails when a page goes stale, a page has no example,
or a link breaks. A new [reference index](../ai-ref/README.md) groups every page by task, and the
root README has a "Find the right doc" table. Release notes (this section) are new too.

### Repository set-up for collaborators
MIT `LICENSE`, `CONTRIBUTING.md`, `CODE_OF_CONDUCT.md`, `SECURITY.md`, `CODEOWNERS`, Dependabot,
a pull-request template, bug and feature issue forms, a Sponsor button, README badges, and
branch protection on `main`.

## Fixed

- **XML: comparing two values compared the elements' names.** `XmlNodeAdapter.DeepEquals` included
  the compared elements' *own* names, so `resolve` could not join `@.productId` to `@.id` in XML
  while JSON and YAML could, and any other value comparison between differently named elements
  failed. An element's name is its key in the parent, not part of its value; only names beneath the
  compared elements count now. `ResolveValueParityTests` joins on differently named keys so the bug
  cannot return.

## Upgrade notes

No breaking changes. Scripts that relied on two differently named XML elements comparing as *not
equal* (for example a `resolve` that matched nothing in XML because the key names differed) will now
match, as they already did in JSON and YAML.

## Pull requests

[#73](https://github.com/SQUORA-NL/TLio/pull/73) · [#74](https://github.com/SQUORA-NL/TLio/pull/74) ·
[#75](https://github.com/SQUORA-NL/TLio/pull/75) ·
[#77](https://github.com/SQUORA-NL/TLio/pull/77) (examples, repository set-up, XML fix)

Previous: [1.2.0](v1.2.0.md)
