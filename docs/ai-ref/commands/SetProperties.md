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

## Verified example

**Writing through an object-key wildcard** — the motivating case: `set`/`add`/`put` cannot write
through `$.widgets.*` (an object-key wildcard resolves fine for a read, but is not
bracket-and-subscript shaped, so none of them treat it as a multi-node write target).
`setProperties` can, because its `path` only has to *select* the matched objects — the write
itself goes through `properties`/`value`, not through the wildcard leaf:

```json
{
  "input": {
    "widgets": {
      "w1": { "status": "active", "qty": 1 },
      "w2": { "status": "inactive", "qty": 2 }
    }
  },
  "script": [
    { "command": "setProperties", "path": "$.widgets.*", "properties": ["status"], "value": "=toArray()" }
  ],
  "result": {
    "widgets": {
      "w1": { "status": ["active"], "qty": 1 },
      "w2": { "status": ["inactive"], "qty": 2 }
    }
  }
}
```

Verified by: `TLio.UnitTests/Fixtures/SetProperties/01-object-key-wildcard/fixture.json`, run
through the same `FixtureTheoryLoader`-driven engine test as the other three commands on this
page (`TLio.UnitTests/Fixtures/FixtureTests.cs::SetProperties`). `$.widgets.*` matches each
property *value* of `widgets` (`w1`, `w2` — the objects, not their names); `status` inside each
is then wrapped, `qty` is left alone.

**Named-array literal `properties`, and the every-property default** — from
`TLio.UnitTests/CommandsTests/SetPropertiesTests.cs`, translated from its C# construction into
the equivalent JSON script (the input/values are copied verbatim from the test):

```json
{
  "input": {
    "person": { "name": "Ada", "tags": "vip", "roles": ["admin"], "address": { "city": "Amsterdam" } }
  },
  "script": [
    { "command": "setProperties", "path": "$.person", "properties": ["tags","roles"], "value": "=toArray()" }
  ],
  "result": {
    "person": { "name": "Ada", "tags": ["vip"], "roles": ["admin"], "address": { "city": "Amsterdam" } }
  }
}
```

Verified by: `SetPropertiesTests.cs::NamedProperties_AreWrapped_OthersUntouched` — `tags` (a
scalar) gets wrapped, `roles` (already an array) passes through unchanged rather than nesting,
`name` (not named) is untouched.

**`=scriptpath(*, kinds, recursive)` as `properties`** — verified by
`SetPropertiesTests.cs::FindFunction_SelectsLiveNodesDirectly`: the same `person` object with
`"properties": "=scriptpath(*,['primitive'],true)"` wraps every primitive at every depth —
`name`, `tags`, and the nested `address.city` — without naming any of them, because find mode
returns live nodes directly rather than names to look up.

## Verified example: turning every complex object in a tree into an array

Ask for `kinds: ['object']` instead of `'primitive'` and the same find-mode call selects every
**complex object**, at every depth, leaving primitives alone. The one thing to get right is
*order*: `=toArray()` deep-clones the node it wraps, so a single call that matches both an object
and a still-unwrapped object nested inside it freezes that child in its pre-wrap shape — the
child's own wrap runs against an orphaned reference and never reaches the document. Run
deepest-first instead, one `setProperties` step per level:

```json
[
  { "command": "setProperties", "path": "$.customer.billing",
    "title": "Wrap the innermost complex objects first",
    "description": "billing.contact must already be an array by the time billing itself is wrapped, or wrapping billing clones it away.",
    "properties": "=scriptpath(*,['object'],true)", "value": "=toArray()" },
  { "command": "setProperties", "path": "$.customer",
    "title": "Now wrap what's left under customer",
    "properties": "=scriptpath(*,['object'],true)", "value": "=toArray()" }
]
```

Given
```json
{
  "customer": {
    "id": "C-1",
    "billing": { "iban": "NL00BANK", "contact": { "email": "ada@example.com" } }
  }
}
```

produces
```json
{
  "customer": {
    "id": "C-1",
    "billing": [ { "iban": "NL00BANK", "contact": [ { "email": "ada@example.com" } ] } ]
  }
}
```

`billing` and the nested `billing.contact` are each now a one-element array; `id` and `iban`
(primitives) are untouched. Running step 2 alone, without step 1 first, still wraps `billing`,
but `contact` comes out unwrapped inside the cloned `billing` — see Common mistakes below.

Verified by (same input tree, both steps): `SetPropertiesTests.cs::RecursiveObjectKind_DeepestFirst_WrapsEveryComplexObjectInTheTree`
(deepest-first, both steps run — matches the result above) and
`::RecursiveObjectKind_SingleCall_LosesTheNestedWrap` (step 2 alone — `contact` stays unwrapped,
the Common mistakes case).

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
- **Wrapping a whole subtree of nested objects in one call**: `=toArray()` deep-clones the node
  it wraps, so if a matched object contains another matched object that has not been wrapped yet,
  the outer wrap clones the inner one in its pre-wrap shape and the inner node's own replacement
  never reaches the document — no warning, no failure, it is just silently lost. Match sets that
  are all siblings (no object nested inside another matched object) are unaffected. For a subtree
  with objects nested inside objects, run one `setProperties` step per level, deepest first — see
  the tree-of-objects example above.
