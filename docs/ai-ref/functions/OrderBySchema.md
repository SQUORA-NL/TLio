# orderBySchema

> Returns a copy of a document with the properties of every object in the order a JSON Schema declares them.

## Syntax

```
=orderBySchema(<schema>, <data>)
=orderBySchema(<schema>, <data>, 'discriminate')
```

## Arguments

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | path or JSON text | yes | The schema. A path to a schema node in the document (`$.schema`) — the schema may live in a JSON, XML or YAML document — or a JSON string (`'{"properties":{"a":{},"b":{}}}'`). |
| 2 | path | yes | The node to order. Inside `forEach`, `@` names the current item. |
| 3 | `'discriminate'` | no | Narrow each `oneOf` / `anyOf` to the branch the data's discriminator selects (see below). Any other third argument is an error. |

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

### `'discriminate'` — tagged unions

A tagged union pins one property to a constant in every branch:

```json
{ "oneOf": [
  { "properties": { "kind": { "const": "car" },   "color": {}, "wheels": {} } },
  { "properties": { "color": {}, "kind": { "const": "plane" }, "wings":  {} } } ] }
```

By default both branches contribute, so a plane's properties are ordered `kind, color, wings`
(the first branch's order wins for shared names). With `'discriminate'` the data's `kind` selects the
plane branch, and the same plane is ordered `color, kind, wings`.

- A **discriminator** is a property pinned by `const` (or `enum`) in **every** branch. The branch is
  chosen when the data has a scalar value for it that **exactly one** branch allows.
- Anywhere it cannot decide — the data is not an object, lacks the property, no branch or several
  allow the value, a branch pins nothing — that choice **stays a union**. Several discriminators are
  tried in the first branch's declaration order. It is decided separately at every level of the data.
- Values compare the same in every format: `1`, `"1"` and `1.0` agree, and so do `true` and `"true"`.
- `then` / `else` are never discriminated (they depend on an `if` that is not evaluated).

Verified by: `Fixtures/Schema/orderbyschema/08-discriminate-picks-the-matching-branch.json`; the
flag-off union `09-…`; no branch matches `10-…`; every level `11-…`; and across JSON, XML and YAML by
`TLio.Parity.Tests/Fixtures/Functions/14-order-by-schema-discriminate`.

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

## In YAML and XML

The same function, the same result; only the paths speak the document's language. Output below is from a real
`tlio_execute` run.

YAML — `{ "command": "put", "path": "$.ordered", "value": "=orderBySchema($.schema, $.data)" }`

```yaml
schema:
  type: object
  properties: { lastName: {}, firstName: {}, age: {} }
data: { age: 30, firstName: John, country: USA, lastName: Doe }
```

adds

```yaml
ordered: { lastName: Doe, firstName: John, age: 30, country: USA }
```

XML — `{ "command": "put", "path": "/root/ordered", "value": "=orderBySchema(/root/schema, /root/data)" }`

```xml
<root>
  <schema><properties><lastName/><firstName/><age/></properties></schema>
  <data><age>30</age><firstName>John</firstName><country>USA</country><lastName>Doe</lastName></data>
</root>
```

adds

```xml
<ordered><lastName>Doe</lastName><firstName>John</firstName><age>30</age><country>USA</country></ordered>
```

An empty element (`<lastName/>`) is the XML spelling of the schema node `{}`. Run with `xmlPathStyle: xpath`; paths name the document element. XML attributes are ignored by the adapter, so `convert` to JSON or YAML first if the schema or data depends on them.

<!-- formats:begin — generated by TLio.Parity.Tests (DocExamples); edit DocExamples/*.json, not this block -->
## Examples in every format

Each example is run through the engine in all three formats; the result shown is what came back. Expand a format to see it.

**basic extras last**

<sub>Test: `TLio.Functions.Tests/Fixtures/Schema/orderbyschema/01-basic-extras-last.json`</sub>

<details>
<summary>JSON</summary>

Input

```json
{
  "schema": {
    "type": "object",
    "properties": {
      "lastName": {},
      "firstName": {},
      "age": {}
    }
  },
  "person": {
    "age": 30,
    "firstName": "John",
    "country": "USA",
    "lastName": "Doe"
  }
}
```

Script

```json
[
  {
    "command": "set",
    "path": "$.person",
    "value": "=orderBySchema($.schema, $.person)"
  }
]
```

Result

```json
{
  "schema": {
    "type": "object",
    "properties": {
      "lastName": {},
      "firstName": {},
      "age": {}
    }
  },
  "person": {
    "lastName": "Doe",
    "firstName": "John",
    "age": 30,
    "country": "USA"
  }
}
```

</details>

<details>
<summary>XML</summary>

Input

```xml
<root>
  <schema>
    <type>object</type>
    <properties>
      <lastName />
      <firstName />
      <age />
    </properties>
  </schema>
  <person>
    <age>30</age>
    <firstName>John</firstName>
    <country>USA</country>
    <lastName>Doe</lastName>
  </person>
</root>
```

Script

```xml
<script>
  <set path="/root/person">
    <value>=orderBySchema(/root/schema, /root/person)</value>
  </set>
</script>
```

Result

```xml
<root>
  <schema>
    <type>object</type>
    <properties>
      <lastName />
      <firstName />
      <age />
    </properties>
  </schema>
  <person>
    <lastName>Doe</lastName>
    <firstName>John</firstName>
    <age>30</age>
    <country>USA</country>
  </person>
</root>
```

</details>

<details>
<summary>YAML</summary>

Input

```yaml
schema:
  type: object
  properties:
    lastName: {}
    firstName: {}
    age: {}
person:
  age: 30
  firstName: John
  country: USA
  lastName: Doe
```

Script

```yaml
- command: set
  path: $.person
  value: '=orderBySchema($.schema, $.person)'
```

Result

```yaml
schema:
  type: object
  properties:
    lastName: {}
    firstName: {}
    age: {}
person:
  lastName: Doe
  firstName: John
  age: 30
  country: USA
...
```

</details>

<!-- formats:end -->

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
- **Expecting `oneOf` to pick a branch by itself.** All branches contribute unless you pass `'discriminate'`. If two branches declare the same property differently, the first declaration decides its position.
- **Expecting `'discriminate'` to validate.** It reads one `const`/`enum` value to choose a branch; a document that matches no branch is simply ordered by the union.
- **Expecting a remote `$ref` to load.** Only `#`-local references are followed.
