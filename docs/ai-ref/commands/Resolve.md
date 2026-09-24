# resolve

> Looks up matching entries in a reference collection and writes derived values to
> the target document. Supports relative `@.property` paths for writing results.

> **ETL extension**: requires `options.CommandsProvider.RegisterETL<TNode>()` in addition
> to `ParseOptions<TNode>.CreateDefault()`.

## Syntax

```json
{
  "command": "resolve",
  "path": "$.orders[*]",
  "settings": [
    {
      "referencesCollectionPath": "$.products",
      "resolveKeys": [
        { "keyPath": "@.productId", "referenceKeyPath": "$.id" }
      ],
      "values": [
        { "targetPath": "@.productName", "value": "=fetch($.name)" }
      ]
    }
  ]
}
```

## Options

| Option | Type | Required | Default | Description |
|--------|------|----------|---------|-------------|
| path | string | yes | — | Selects the nodes to resolve (the "left side"). |
| settings | array | yes | — | Array of resolve setting objects (see Settings below). |

### Resolve setting object

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| referencesCollectionPath | string | yes | Path to the reference collection to search. |
| resolveKeys | array | yes | Array of `{keyPath, referenceKeyPath}` join conditions. |
| values | array | yes | Array of `{targetPath, value}` to write when a match is found. |

- `keyPath`: path relative to the current node (`@.property`) — the dot after `@` is always required
- `referenceKeyPath`: path in the reference entry (absolute or `$.property`)
- `targetPath`: where to write the result — `@.property` writes relative to current node; a
  function expression (`"=..."`) instead **computes** the property name — see below
- `value`: `@.property` reads it off the **matched reference entry**; anything else — a literal,
  a function expression, an absolute path — is evaluated the ordinary way

### A dynamic `targetPath` — property names driven by a lookup table

Written as a plain string, `targetPath` names the property literally. Written as a function
expression (starts with `"="`, e.g. `"=fetch(@.to)"`), it instead **computes** the name by
evaluating the expression against the matched reference entry — exactly the way `value` is
evaluated. This is what lets one `resolve` rename or remap many properties from a table of
`{from, to}` pairs, instead of one `values` entry per property:

```json
{
  "command": "resolve",
  "path": "$.node",
  "resolveSettings": [{
    "referencesCollectionPath": "$.table[*]",
    "resolveKeys": [{ "keyPath": "@._code", "referenceKeyPath": "@.code" }],
    "values": [{ "targetPath": "=fetch(@.to)", "value": "=fetch(@.from)" }]
  }]
}
```

Requires exactly one match. With zero matches there is nothing to name the property after; with
more than one there is no defined meaning for writing several differently-named properties from
one `targetPath` expression. Either case is a warning and a skip — resolve never throws — and the
target must be an object (a dynamic name has nowhere to go on a scalar or array target).

> See [Notation Reference](../notation-reference.md) for relative-path rules.

### `@.` is the same in every format

`@.property` inside a resolve setting belongs to the *script*, not to the document, so it is
written the same way whatever format the data is in — `@.label`, never `./label`, even in XML.
All four places that take it (`keyPath`, `referenceKeyPath`, `targetPath`, `value`) read it off
the node with the adapter, and `.` separates the steps of a walk: `@.detail.tier` is
`detail` then `tier`, in XML as much as in JSON.

It cannot be a path in the document's own language, because the matched entry is not something a
path can name — `referencesCollectionPath` matched several nodes and this is one of them. In XML
they all share an absolute path, so a path-based read would return the first sibling rather than
the match.

> **This uniform `@.` is the *bare* form only — a `targetPath`/`value` written as a function
> expression does not get it.** `"targetPath": "@.to"` is resolve's own reader, hardcoded to `@.`
> regardless of format. `"targetPath": "=fetch(@.to)"` is a function call; the `@.to` inside it is
> just another argument, resolved the *ordinary* way — through the document format's own relative
> marker, not resolve's. That marker is `@.` for JSON and YAML, but **`.`** for XML (matching
> XPath's own "current node" dot, the same one `decisionTable`'s XML inputs already use as
> `./status`). Writing `=fetch(@.to)` in an XML script does not fail loudly: `@.to` is simply not
> a path XML's fetcher recognises, so the function returns the literal text `"@.to"` — a target
> named `<@.to>` cannot be created, so the write silently does nothing. Write `=fetch(./to)` for
> XML. This is exactly the trap the cross-format sweep test (`TLio.Parity.Tests`) caught: two
> attempts at the "obvious" JSON-style spelling both passed for JSON and YAML and both silently
> did nothing in XML, and only the sweep's per-format apply-and-compare noticed.

> **`sourcePath` is not a key `resolve` reads.** A `values` entry written
> `{"sourcePath": "@.label", "targetPath": "@.label"}` binds no value and writes nothing, with no
> warning. The key is `value`.

**Functions in the value**: — no value field  
**Functions in the path**: — not resolved here; resolve it in a preceding step

## Notes

- The JSON key for the settings array is `"resolveSettings"` (the `settings` field name in JSON).
- The join is indexed once per `resolveSettings` entry and reused for every node `path` selects —
  runtime scales with the number of targets, not with `targets × references`. This holds for a
  scalar `keyPath`/`referenceKeyPath` pair (the common case); an array-based (`[*]`) key still
  falls back to an exact scan, as it always has.

## Formats

Works with all adapters. Path syntax differs per adapter — see [overview.md](../overview.md).

## When to use

- Enriching a collection with data from a lookup/reference collection — the classic foreign-key join pattern: for each item in a source array, find the matching entry in a reference array and copy fields onto the source item.
- Denormalising data for export: pulling product details onto order lines, user display names onto event records, country names onto address objects, etc.
- The join key exists on the source item and the reference collection is present in the same document (or can be loaded into it beforehand).

## When NOT to use

- You need to compute a derived value rather than look one up — use functions (`set` with an expression) instead of `resolve`.
- The reference data is not in the document — load it first with a preceding command (e.g., `copy` from an external source), then resolve against it.
- Only one specific value needs copying and the path is already known — a single `copy` command is simpler and clearer than a full resolve.
- The join is many-to-many — `resolve` writes the first match; multiple matches are not currently aggregated.

## Common mistakes

- **Confusing `@.field` with `$.field` in `value`**: the `value` expression (e.g., `"@.category"`) resolves relative to the **matched reference entry**, not the source node and not the document root. To reference a field on the document root use `"$.root.path"`. This is the most common source of wrong or empty values.
- **Wrong path in `keyPath`**: `keyPath` must be relative to the current source node, written as `"@.productId"` (with the dot). The `@` without a dot is not valid in JSON/YAML contexts.
- **`referencesCollectionPath` points to a single object instead of a collection**: resolve iterates the collection and matches by key; pointing to a single object rather than an array will not produce matches.
- **Expecting resolve to merge entire reference objects**: `values` must explicitly list every field to copy. Resolve does not auto-merge the full reference entry onto the source node — use `merge` for that after resolving the match.
- **Using the JSON key name incorrectly**: the settings array is keyed as `"resolveSettings"` in JSON, not `"settings"`. Using `"settings"` will cause the command to fail or be silently ignored.

## Failure modes and what the trace tells you

- **Target fields remain absent**: trace shows resolve ran but no fields were written. The `keyPath`/`referenceKeyPath` join produced no matches — verify the key values exist on both sides and the types match (string vs. number mismatches prevent equality).
- **`@.field` resolves to null**: the `value` expression is being evaluated relative to the reference entry, and the field name is wrong or the reference object uses a different property name. Inspect the reference entry shape in the trace.
- **ETL extension not registered**: trace reports an unknown command `resolve`. Ensure `RegisterETL<TNode>()` is called on the commands provider at startup.
- **All source nodes unmatched**: `referencesCollectionPath` resolves to an empty array or the path is wrong. Check the document state at the point the command runs using the trace's node snapshots.
