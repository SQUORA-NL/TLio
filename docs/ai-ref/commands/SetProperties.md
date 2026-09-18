# setProperties

> Runs `value` against a selection of nodes under the object(s) matched by `path`, replacing
> each one with the result — `set`, but keyed by an explicit selection instead of a single path
> expression. Exists because `set`/`add`/`put` cannot write through an object-key wildcard
> (`$.obj.*`) or a multi-key union (`$.obj['a','b']`) — both resolve fine for a read, neither
> for a write.

## Syntax

```json
{ "command": "setProperties", "path": "$.person", "properties": ["tags","roles"], "value": "=toArray()" }
{ "command": "setProperties", "path": "$.person", "value": "=toArray()" }
```

> See [Notation Reference](../notation-reference.md) for quoting rules and escape sequences.

## Options

| Option | Type | Required | Default | Description |
|--------|------|----------|---------|-------------|
| path | string | yes | — | Selects the object(s) to operate on. A wildcard container path (`$.items[*]`) applies the same selection and value to every match. |
| properties | array of strings, or a function | no | every direct property | What to touch under each matched object — see below. |
| value | function | yes | — | Evaluated once per selected node, with that node's own current value as the current node (`=toArray()` called bare wraps exactly that value). Any function works — this command has no array-specific logic of its own. |

### `properties` — two shapes

- **A literal array** of strings, each one either a bare name (`"tags"`) or an `@.`-relative
  path (`"@.address.city"`) — resolved the same way `scriptpath(@.child)` already resolves a
  relative path, so a nested sub-item is reachable, not just a direct child.
- **A function**, typically `=scriptpath(*, kinds, recursive)` (see
  [ScriptPath.md](../functions/ScriptPath.md#find-mode-scriptpath-kinds-recursive)) — its result
  is used as the live set of target nodes directly, no name lookup at all.

Omitted, or an empty result either way, means every direct property of the matched object.

**Functions in the value**: ✅ any function
**Functions in properties**: ✅ any function
**Functions in the path**: ✅ `=indirect()` in `path`

## Formats

Works with all adapters through `INodeAdapter`/`IItemsFetcher`. A node matched by `path` that is
not an object is skipped with a warning (nothing to set properties on). A named property that
does not exist, or a relative path that matches nothing, is skipped with a warning; the command
still succeeds — a missing name is an answer about the document, not a script error.

## Example

```json
[
  { "command": "setProperties", "path": "$.customer", "properties": ["email","vip"], "value": "=toArray()" },
  { "command": "setProperties", "path": "$.orders[*]", "value": "=toArray()" },
  { "command": "setProperties", "path": "$.customer",
    "properties": "=scriptpath(*,['primitive'],true)", "value": "=toArray()" }
]
```

Given
```json
{
  "customer": { "id": "C-1", "email": "a@b.com", "vip": true, "address": { "city": "Amsterdam" } },
  "orders": [ { "id": "O-1", "status": "shipped" } ]
}
```

- Step 1 wraps only `email` and `vip`.
- Step 2 wraps every property of every matched order (`$.orders[*]` matches one order here).
- Step 3 (run standalone, not chained after 1–2) wraps every primitive anywhere under `customer`
  — including the nested `address.city` — without naming `address` at all.

## C# Usage

```csharp
var options = ParseOptions<JToken>.CreateDefault();
var value = new FunctionSupportedValue<JToken>(new ToArray<JToken>());
var properties = new FixedValue<JToken>(new JArray("tags", "roles"));
var cmd = new SetProperties<JToken>("$.person", value, properties);
```

## When to use

- Normalising several named fields to a consistent shape (e.g. always-array) without a separate
  `set` command per field.
- Applying the same value/function to every property of an object, or every property of every
  object a wildcard path matches — no other command reaches an object-key wildcard.
- Reaching a nested sub-item (`@.address.city`) in the same list as top-level names, or finding
  every matching node in a subtree by kind (`=scriptpath(*, kinds, recursive)`) without knowing
  the field names in advance.

## When NOT to use

- **A single, known path** — a plain `set`/`put` is simpler and clearer when there is exactly one
  target.
- **Growing an existing array** — this command replaces each selected node's value; it does not
  append. Use `add` with a trailing index, or `merge`.
- **The transform is always "wrap in an array"** — `setProperties` is generic on purpose;
  `=toArray()` is just the value that happens to be passed in. If that is genuinely the only use,
  the script reads the same either way — nothing simpler is lost by keeping `value` explicit.

## Comparison

| Tool | Effect | Use when |
|------|--------|----------|
| `setProperties` | Runs a value function against a named/found selection under one or more objects | Multiple named or discovered targets, one script step |
| `set` | Runs a value function against one path expression | A single, ordinary path — including array wildcards (`$.items[*]`), which `set` already handles |
| `=scriptpath(*, kinds, recursive)` | Finds nodes by kind, recursively | The selection itself, used as `properties` here or by any other consumer of a node set |

## Common mistakes

- **Expecting `properties` to append or merge**: it only selects *which* existing nodes to
  overwrite; it does not add new properties. A name that does not exist is skipped with a
  warning, not created.
- **Passing a bare string instead of an array**: `"properties": "tags"` is not the same as
  `"properties": ["tags"]` — a literal selection must be a JSON array, even for one name.
- **Assuming `kinds: ['object']` stops the walk**: in find mode, whether an object is included in
  the results and whether the walk descends into it are independent — `recursive` alone controls
  depth.
