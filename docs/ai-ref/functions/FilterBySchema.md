# filterBySchema

> Returns a copy of a document holding only the properties a JSON Schema describes.

## Syntax

```
=filterBySchema(<schema>, <data>)
=filterBySchema(<schema>, <data>, 'keepAdditional')
=filterBySchema(<schema>, <data>, 'discriminate')
=filterBySchema(<schema>, <data>, 'discriminate', 'keepAdditional')
```

## Arguments

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | path or JSON text | yes | The schema — a path to a schema node (`$.schema`; JSON, XML or YAML) or a JSON string. |
| 2 | path | yes | The node to filter. Inside `forEach`, `@` names the current item. |
| 3, 4 | `'keepAdditional'`, `'discriminate'` | no | Flags, in either order. `'keepAdditional'` keeps properties the schema does not list, unless it says `additionalProperties: false`. `'discriminate'` narrows each `oneOf` / `anyOf` to the branch the data's discriminator selects. |

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

### `'discriminate'` — tagged unions

By default a `oneOf` keeps the properties of **every** branch, so a car carrying a stray `wings`
keeps it. When each branch pins a property with `const` (or `enum`) — `kind: "car"` vs `kind: "plane"` —
`'discriminate'` reads the data's value and keeps only the matching branch's properties, so the
car loses `wings`.

- A **discriminator** is a property pinned in **every** branch; the branch is chosen when the data's
  scalar value for it is allowed by **exactly one** branch (`enum` lists work: `["car","van"]`).
- Wherever it cannot decide — data not an object, property missing, no branch or several match, a
  branch pins nothing — that choice **stays a union**, so `'discriminate'` can only ever remove
  *more* than the default when it is sure. It is decided separately at every level.
- `then` / `else` are never discriminated.

Verified by: `Fixtures/Schema/filterbyschema/07-discriminate-drops-the-other-branch.json`; flag off
`08-…`; `enum` `09-…`; both flags `10-…`; and across JSON, XML and YAML by
`TLio.Parity.Tests/Fixtures/Functions/15-filter-by-schema-discriminate` and `16-…-enum`.

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

## In YAML and XML

YAML — `{ "command": "put", "path": "$.filtered", "value": "=filterBySchema($.schema, $.data)" }`

```yaml
schema:
  properties:
    id: {}
    address: { properties: { city: {} } }
data: { id: 1, secret: x, address: { city: Delft, zip: "2611" } }
```

adds

```yaml
filtered: { id: 1, address: { city: Delft } }
```

XML — `{ "command": "put", "path": "/root/filtered", "value": "=filterBySchema(/root/schema, /root/data)" }`

```xml
<root>
  <schema><properties><id/><address><properties><city/></properties></address></properties></schema>
  <data><id>1</id><secret>x</secret><address><city>Delft</city><zip>2611</zip></address></data>
</root>
```

adds

```xml
<filtered><id>1</id><address><city>Delft</city></address></filtered>
```

Run with `xmlPathStyle: xpath`; paths name the document element. XML attributes are ignored by the adapter, so `convert` to JSON or YAML first if the schema or data depends on them.

## When to use

- Stripping internal fields before a document leaves a boundary — the schema is the allow-list.
- Reducing a rich document to the contract a consumer publishes.

## When NOT to use

- To remove a *known* field — `remove` says so directly.
- To *validate* — a document that breaks the schema's types or `required` list passes through unchanged.

## Common mistakes

- **Omitting the data argument.** JLio's one-argument form is not supported; see `orderBySchema`.
- **Expecting a free-form object to be emptied.** `{"type":"object"}` with no `properties` keeps its contents.
- **Expecting `oneOf` to select a branch by itself.** The union of all branches is kept unless you pass `'discriminate'`.
- **Using `'discriminate'` on a union with no `const`/`enum` tag.** It has nothing to read and quietly stays a union.
- **Putting `'keepAdditional'` and expecting `additionalProperties: false` to be overridden.** It is honoured.
