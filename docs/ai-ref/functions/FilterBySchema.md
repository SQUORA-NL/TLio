# filterBySchema

> Returns a copy of a document holding only the properties a JSON Schema describes.

## Syntax

```
=filterBySchema(<schema>, <data>)
=filterBySchema(<schema>, <data>, 'keepAdditional')
```

## Arguments

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | path or JSON text | yes | The schema — a path to a schema node (`$.schema`; JSON, XML or YAML) or a JSON string. |
| 2 | path | yes | The node to filter. Inside `forEach`, `@` names the current item. |
| 3 | `'keepAdditional'` | no | Keep properties the schema does not list, unless it says `additionalProperties: false`. |

## Returns

A **new node**; the source is never modified. Every object described by `properties` loses the
properties that schema does not list, recursively through `properties` and `items` / `prefixItems`.
Values and the order of what remains are untouched.

- **Default: drop.** Anything the schema does not list is removed, as in JLio.
- **Opaque values are kept whole.** A property whose schema says nothing about shape (`{}`, or only
  `{"type":"object"}`) is a free-form value, not an undescribed one — its contents are not stripped.
  (JLio compares paths and *would* strip them; this is a deliberate refinement.)
- **`'keepAdditional'`**: unlisted properties survive. `additionalProperties: false` still removes
  them; an `additionalProperties` *schema* is applied to them.
- **Composition is a union.** A property described by any `allOf` member or any `oneOf` / `anyOf` /
  `then` / `else` branch is kept. Choosing one branch without validating would silently drop data.
- `$ref` is followed when local (`#`, `#/$defs/…`, `#/definitions/…`). This is not a validator:
  `type`, `required` and the like are never evaluated.

## Verified example

```json
{ "command": "put", "path": "$.filtered", "value": "=filterBySchema($.schema, $.data)" }
```

Input:

```json
{
  "schema": { "properties": { "id": {}, "address": { "properties": { "city": {} } } } },
  "data": { "id": 1, "secret": "x", "address": { "city": "Delft", "zip": "2611" } }
}
```

Output adds:

```json
{ "filtered": { "id": 1, "address": { "city": "Delft" } } }
```

Verified by: `TLio.Functions.Tests/Fixtures/Schema/filterbyschema/01-drops-undescribed.json`, and across
JSON, XML and YAML by `TLio.Parity.Tests/Fixtures/Functions/11-filter-by-schema`.
Opaque values: `02-opaque-value-is-kept.json`. `'keepAdditional'`: `03-keep-additional.json`,
and with `additionalProperties: false`: `04-keep-additional-honours-false.json`. `anyOf` as a
union: `05-anyof-keeps-union.json`. In place on the same path: `06-in-place-on-the-same-path.json`.

## When to use

- Stripping internal fields before a document leaves a boundary — the schema is the allow-list.
- Reducing a rich document to the contract a consumer publishes.

## When NOT to use

- To remove a *known* field — `remove` says so directly.
- To *validate* — a document that breaks the schema's types or `required` list passes through unchanged.

## Common mistakes

- **Omitting the data argument.** JLio's one-argument form is not supported; see `orderBySchema`.
- **Expecting a free-form object to be emptied.** `{"type":"object"}` with no `properties` keeps its contents.
- **Expecting `oneOf` to select a branch.** The union of all branches is kept.
- **Putting `'keepAdditional'` and expecting `additionalProperties: false` to be overridden.** It is honoured.
