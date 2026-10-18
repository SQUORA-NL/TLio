# orderBySchema

> Returns a copy of a document with the properties of every object in the order a JSON Schema declares them.

## Syntax

```
=orderBySchema(<schema>, <data>)
```

## Arguments

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | path or JSON text | yes | The schema. A path to a schema node in the document (`$.schema`) — the schema may live in a JSON, XML or YAML document — or a JSON string (`'{"properties":{"a":{},"b":{}}}'`). |
| 2 | path | yes | The node to order. Inside `forEach`, `@` names the current item. |

## Returns

A **new node**; the source is never modified. Nothing is added, removed or changed — only the order
of properties differs.

- Properties the schema declares come first, **in the order `properties` declares them**.
- Properties the schema does not mention are kept and go **last**, in their original relative order.
- Recursion follows `properties`, `items` (one schema for every element), `prefixItems` / tuple
  `items` (by position), and `additionalProperties` when it is a schema.
- Values the schema says nothing about are left as they are.

### Which schema keywords are read

This is **not a validator**. `type`, `required`, `pattern`, `minimum` and the rest are never evaluated.
Only the keywords that decide shape are read:

| Keyword | Effect |
|---|---|
| `properties` | Declares names, and their order. |
| `items`, `prefixItems` | The schema of array elements. |
| `$ref` | **Local only** — `#` or `#/$defs/Name` (also `#/definitions/Name`). A remote reference is ignored with a warning. Recursive schemas are fine. |
| `allOf` | Every member contributes, in order. |
| `oneOf`, `anyOf`, `then`, `else` | **Union**: every branch contributes, in branch order. Without validation there is no knowing which branch the data matches, and a union never loses a property. A property declared by two branches keeps the position of the first. |
| `if`, `not`, `patternProperties`, `propertyNames` | Ignored. |

## Verified example

```json
{ "command": "put", "path": "$.ordered", "value": "=orderBySchema($.schema, $.data)" }
```

Input:

```json
{
  "schema": { "type": "object", "properties": { "lastName": {}, "firstName": {}, "age": {} } },
  "data": { "age": 30, "firstName": "John", "country": "USA", "lastName": "Doe" }
}
```

Output adds:

```json
{ "ordered": { "lastName": "Doe", "firstName": "John", "age": 30, "country": "USA" } }
```

Verified by: `TLio.Functions.Tests/Fixtures/Schema/orderbyschema/01-basic-extras-last.json`, and across
JSON, XML and YAML by `TLio.Parity.Tests/Fixtures/Functions/09-order-by-schema`.
Nested objects and array items: `03-nested-and-array-items.json` / parity `10-…`. `$ref` with
`allOf`: `04-ref-and-allof.json`. `oneOf` as a union: `05-oneof-is-a-union.json`. A recursive
schema: `06-recursive-ref.json`. A schema given as JSON text: `07-schema-as-json-text.json`.

## When to use

- Normalising property order before serialising — API responses, XML that a consumer reads positionally, diff-friendly output.
- A schema is the one description of the document you already have and you want the document to follow it.

## When NOT to use

- You need to drop what the schema does not describe — that is `filterBySchema`.
- You need to *validate* — nothing here checks types or required fields.
- You want to sort **array elements** by a value — that is `sort` / `sortby`. `orderBySchema` orders properties; array element order is never changed.

## Comparison

| Tool | Changes | Leaves |
|---|---|---|
| `orderBySchema` | property order | contents |
| `filterBySchema` | which properties exist | order of what remains |
| `skeleton` | builds a new empty document | — |
| `sortby` | order of array elements | contents |

## Common mistakes

- **Omitting the data argument.** JLio's one-argument form ("shape the node being set") is not supported: in TLio's `set` the node a function is evaluated against is the target's *parent*, so it would shape the wrong object. Pass the data path explicitly.
- **Expecting `oneOf` to pick a branch.** All branches contribute. If two branches declare the same property differently, the first declaration decides its position.
- **Expecting a remote `$ref` to load.** Only `#`-local references are followed.
