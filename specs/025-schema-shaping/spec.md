# Feature Specification: Schema Shaping (orderBySchema, filterBySchema, skeleton)

**Feature Branch**: `feature/match-jlio-support`
**Status**: Phases 1 and 2 implemented
**Origin**: JLio's `JLio.Extensions.JSchema` pack (`filterBySchema`, `orderBySchema`), deferred from
spec 002 because the schema library JLio uses (Newtonsoft.Json.Schema) is AGPL / paid and bound to
Newtonsoft. TLio has to run without Newtonsoft, so the *functionality* is wanted, not the implementation.

## Decisions (agreed)

| # | Question | Decision |
|---|---|---|
| 1 | `filterBySchema` on properties the schema does not list | **Drop** (JLio parity). `'keepAdditional'` is an opt-in that honours `additionalProperties`. |
| 2 | Is `skeleton` in scope? | **Yes**, phase 1. |
| 3 | `$ref` | **Local only** (`#`, `#/$defs/…`, `#/definitions/…`). Remote references are ignored with a warning. |
| 4 | `oneOf` / `anyOf` / `allOf` | `allOf` and every branch of `oneOf`/`anyOf`/`then`/`else` **union**; `skeleton` commits to the first branch. |
| 5 | Discriminator-based branch selection | **Phase 2**, opt-in via `'discriminate'`; phase 1 was built to take it without reworking the traversal. |

## What JLio actually does with a schema

Neither function validates. Both read the schema as a tree of names: `orderBySchema` walks `properties`
(declaration order) and `items`; `filterBySchema` collects every path the schema describes and removes
the rest. The library is only used to parse that tree. That is what makes a dependency-free
implementation possible — the schema needs to be *read*, not *evaluated*.

## Requirements

### Functions (all built in, registered by `ParseOptions`; no new package)

- **FR-1** `=orderBySchema(schema, data)` returns a copy of `data` with every object's properties in
  schema-declared order; undeclared properties are kept after the declared ones in original order.
  Nothing is added, removed or changed.
- **FR-2** `=filterBySchema(schema, data [, 'keepAdditional'])` returns a copy holding only what the
  schema describes. A value whose schema says nothing about shape (`{}`, `{"type":"object"}`) is kept
  whole.
- **FR-3** `=skeleton(schema)` returns the empty document the schema describes: declared properties in
  order, `[]` for arrays, `default`/`const` where given, `null` elsewhere. Recursive schemas terminate.
- **FR-4** The schema argument is a **path to a schema node in the document** (JSON, XML or YAML) or
  **JSON text**. It is read through `INodeAdapter`, never through a format-specific parser.
- **FR-5** The data argument is explicit. JLio's implicit "the node being set" form is not supported:
  in TLio's legacy `set` the node a function is evaluated against is the target's parent
  (`behaviour-decisions.md` section C).
- **FR-6** The source document is never modified; results are fresh clones.

### Schema keywords read

`properties`, `items`, `prefixItems`, `additionalProperties`, `$ref` (local), `allOf`, `oneOf`, `anyOf`,
`then`, `else`, `default`, `const`, `enum`, `type` (skeleton only). Everything else — including `if`,
`not`, `patternProperties`, `required`, `pattern`, `minimum` — is ignored. **This is not a validator.**

### Composition rule (decision 4)

Without validation the matching branch cannot be known, and choosing one would silently drop data from
`filterBySchema`. So `oneOf`/`anyOf`/`then`/`else` branches all contribute (union, in branch order); a
property declared twice keeps the position of its first declaration. `skeleton` is the exception — a blank
document cannot be two shapes — and takes the first branch.

### Cross-format behaviour

- **FR-7** Identical output in JSON (both adapters), XML and YAML. Proven by `TLio.Parity.Tests`
  (fixtures `Functions/09`–`13` and the sweep) and `TLio.Json.AdapterParity.Tests`.
- **FR-8** No new adapter method: reordering is remove-then-set, which appends in every format.
- **FR-9** Tests that assert order compare serialised text; `JToken.DeepEquals` is order-insensitive.

## Phase 2 — discriminator selection (implemented)

A tagged union is written as branches that pin one property to a constant:

```json
{ "oneOf": [
  { "properties": { "kind": { "const": "car" },   "wheels": {} } },
  { "properties": { "kind": { "const": "plane" }, "wings":  {} } } ] }
```

With the data in hand, the matching branch is decidable without validating: read the property that every
branch pins with `const`/`enum`, and keep the branch whose values include the data's. Where it is not
decidable (no such property, the data lacks it, several branches match) fall back to the union.

### How phase 1 prepares for it

| Phase 2 needs | Already in phase 1 |
|---|---|
| The data node at the point of choice | `SchemaView.Flatten(data)` passes it to `IBranchSelector.Select(kind, alternatives, data)` at every level of every traversal. |
| The `const` / `enum` values of each branch | `SchemaNode.AllowedValues`, read by `SchemaDocument`. |
| A place to plug the rule in | `IBranchSelector<TNode>`; phase 1 ships `UnionBranchSelector` and `FirstBranchSelector`. |
| A pinned test of the seam | `SchemaShapeTests.BranchSelector_ReceivesTheDataNode_AndTheBranchKindsConstValues`. |

Phase 2 therefore adds one class (`DiscriminatorBranchSelector<TNode>`), a way to choose it (a trailing
option on the functions, `'discriminate'`), docs and fixtures — no change to `SchemaDocument` or
`SchemaShaper`. The default stays the union. The one touch to phase 1's reader was a read-only
accessor, `FlatSchema.AllowedValues` (all `const`/`enum` values of an expanded property), because a
property pinned across `allOf` members is only visible once flattened.

### As built

- **FR-10** A **discriminator** is a property pinned (`const`, or `enum`) in **every** `oneOf`/`anyOf`
  branch. When the data holds a scalar value for it that **exactly one** branch allows, that branch is
  the only one that contributes.
- **FR-11** Where that cannot be decided — data not an object, property missing or not a scalar, no
  branch or several allow the value, a branch pins nothing — the choice **stays a union**. Further
  discriminators are tried in the first branch's declaration order before giving up. So `'discriminate'`
  never removes anything it is not sure about.
- **FR-12** Branches are read through `$ref` and `allOf`; the choice is made independently at every level
  of the data. `then`/`else` are never discriminated (they hang on an `if` that is not evaluated).
- **FR-13** Values compare the same in every format: `1`, `"1"`, `1.0` agree; `true` and `"true"` agree.
- **FR-14** Opt-in: `orderBySchema(schema, data, 'discriminate')`;
  `filterBySchema(schema, data, 'discriminate' [, 'keepAdditional'])`, flags in either order. A third
  argument to `orderBySchema` that is not `'discriminate'` is an error. `skeleton` is unchanged (first branch).

Tests: `Fixtures/Schema/orderbyschema/08`–`11`, `filterbyschema/07`–`10`, `SchemaShapeTests`
(discriminator section), parity `Functions/14`–`16`.

## Not in scope

- Validation of any kind (`type`, `required`, formats, ranges). If wanted later it is a separate command.
- Remote `$ref`, `$dynamicRef`, `$anchor`.
- `patternProperties` / `propertyNames`.

## Success criteria

- JLio's `orderBySchema` examples (basic, nested, array items) produce the same output.
- The same schema and data give the same result in JSON, XML and YAML.
- No reference to Newtonsoft, a schema library, or any new package from `TLio.Functions`.
- A recursive schema (`Node { children: [Node] }`) neither hangs nor overflows, in any of the three functions.

## Files

- `TLio.Functions/Schema/` — `SchemaNode`, `SchemaDocument`, `SchemaView` (+ `FlatSchema`), `IBranchSelector`,
  `SchemaShaper`, `SchemaFunctionBase`, `OrderBySchema`, `FilterBySchema`, `Skeleton`.
- Docs: `docs/ai-ref/functions/{OrderBySchema,FilterBySchema,Skeleton}.md`; `docs/behaviour-decisions.md` §C.
- Tests: `TLio.Functions.Tests/Fixtures/Schema/`, `…/FunctionsTests/SchemaTests/SchemaShapeTests.cs`,
  `TLio.Parity.Tests/Fixtures/Functions/09`–`13`, `Sweep/`.
