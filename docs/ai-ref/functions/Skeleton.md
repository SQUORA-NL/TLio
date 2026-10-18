# skeleton

> Builds the empty document a JSON Schema describes: declared properties in order, empty arrays, schema defaults, null elsewhere.

## Syntax

```
=skeleton(<schema>)
```

## Arguments

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | path or JSON text | yes | The schema — a path to a schema node (`$.schema`; JSON, XML or YAML) or a JSON string. |

## Returns

A **new node**:

- An object (`properties` present, or `type: "object"`) with **every declared property, in declaration order**, each built the same way.
- An empty array where the schema says array (`type: "array"` or `items`).
- The schema's own `default` — or its `const` — wherever it gives one. A default wins over structure.
- `null` everywhere else.

Composition: `allOf` members are all included; a `oneOf` / `anyOf` / `then` / `else` commits to its
**first** branch, because a blank document cannot be two shapes at once. A recursive schema is cut
where it loops back on itself (an object property becomes `null`, an array is empty), so
`Node { next: Node }` terminates. `$ref` is local only.

## Verified example

```json
{ "command": "put", "path": "$.blank", "value": "=skeleton($.schema)" }
```

Input:

```json
{
  "schema": {
    "properties": {
      "id": { "type": "integer" },
      "status": { "type": "string", "default": "new" },
      "tags": { "type": "array", "items": { "type": "string" } },
      "address": { "properties": { "city": {}, "zip": {} } }
    }
  }
}
```

Output adds:

```json
{ "blank": { "id": null, "status": "new", "tags": [], "address": { "city": null, "zip": null } } }
```

Verified by: `TLio.Functions.Tests/Fixtures/Schema/skeleton/01-shape-defaults-and-arrays.json`, and across
JSON, XML and YAML by `TLio.Parity.Tests/Fixtures/Functions/13-skeleton-defaults`.
First-branch commitment: `02-oneof-commits-to-first-branch.json`. Recursion:
`03-recursive-schema-terminates.json`.

## In YAML and XML

YAML — `{ "command": "put", "path": "$.blank", "value": "=skeleton($.schema)" }`

```yaml
schema:
  properties:
    id: { type: integer }
    status: { type: string, default: new }
    tags: { type: array, items: { type: string } }
    address: { properties: { city: {}, zip: {} } }
```

adds

```yaml
blank: { id: null, status: new, tags: [], address: { city: null, zip: null } }
```

XML — `{ "command": "put", "path": "/root/blank", "value": "=skeleton(/root/schema)" }`, with the same schema written as elements
(`<status><type>string</type><default>new</default></status>`, …), adds

```xml
<blank><id/><status>new</status><tags/><address><city/><zip/></address></blank>
```

In XML an empty element is null, `""`, `{}` and `[]` at once, so `<id/>` and `<tags/>` are how `null` and `[]` are written; the
distinction returns if you `convert` the result to JSON or YAML. Run with `xmlPathStyle: xpath`; paths name the document element. XML attributes are ignored by the adapter, so `convert` to JSON or YAML first if the schema or data depends on them.

## With `merge`

A skeleton works on either side of a `merge`.

**As the target** — the data wins, the schema's defaults fill the gaps and its property order leads
(top-level objects only):

```json
[
  { "command": "put",   "path": "$.defaults", "value": "=skeleton($.schema)" },
  { "command": "merge", "fromPath": "$.doc", "toPath": "$.defaults" }
]
```

Verified by: `TLio.Functions.Tests/Fixtures/Schema/skeleton/04-merge-applies-defaults.json`.

**As the source with `onlyStructure`** — apply the schema's defaults to a document without ever
touching a value it already has; properties it lacks are added, existing ones (and the document's own
order) are kept:

```json
[
  { "command": "put",   "path": "$.blank", "value": "=skeleton($.schema)" },
  { "command": "merge", "fromPath": "$.blank", "toPath": "$.doc",
    "settings": { "strategy": "onlyStructure" } }
]
```

Verified by: `05-merge-only-structure-fills-gaps.json`. (Do not use `onlyStructure` with the skeleton
as the *target*: the skeleton's `null`s are existing values, so the data would never be applied.)

## When NOT to use

- To order or strip an *existing* document — `orderBySchema` / `filterBySchema`. A skeleton cannot shape an array of objects: it has no per-element content, so merging into one cannot reorder the elements' properties.

## Common mistakes

- **Expecting `required` to matter.** Every declared property is included; `required` is not read.
- **Expecting a typed zero.** `{"type":"integer"}` yields `null`, not `0`. Give the schema a `default` if you want a value.
