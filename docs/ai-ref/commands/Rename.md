# rename

> Changes the name under which a node is known, keeping its value, children and position.
> This is the operation `copy` + `remove` cannot express: it preserves order and XML
> attributes, and it reaches the XML document element, which has no parent to copy out of.
> Logs a warning and continues if the path matches nothing or the node has no name.

## Syntax

```json
{ "command": "rename", "path": "/order/customer", "name": "client" }
```

> See [Notation Reference](../notation-reference.md) for quoting rules and escape sequences.

## Options

| Option | Type | Required | Default | Description |
|--------|------|----------|---------|-------------|
| path | string | yes | — | Selects the node(s) to rename. Wildcards and recursive descent rename every match. |
| name | string | yes | — | The new name. A literal — functions are not evaluated here. |

**Functions in the value**: — `name` is a literal  
**Functions in the path**: ✅ `=indirect()` in `path`

## Formats

Where a node's name lives is format-specific, and that decides what can be renamed:

| Format | The name belongs to | Root renameable? |
|--------|--------------------|------------------|
| XML | the element itself | ✅ — `<order>` → `<opdracht>` |
| JSON (Newtonsoft) | the parent property | ❌ — a root object has no property |
| JSON (System.Text.Json) | the parent object's key | ❌ — same |
| YAML | the parent mapping key | ❌ — same |

Renaming a JSON/YAML root, or an array/sequence element (named by position, not by a key),
logs a warning and changes nothing. It is not a failure.

## Verified example

### JSON — renaming an object property

Input:

```json
{ "a": { "x": 1 } }
```

Script:

```json
[{ "command": "rename", "path": "$.a", "name": "z" }]
```

Result:

```json
{ "z": { "x": 1 } }
```

Verified by: `TLio.Parity.Tests/Fixtures/Rename/02-rename-object/fixture.json`

### XML — renaming the document element itself

This is the case called out above: `path` still names the element being renamed
(`/order`), and because XML elements carry their own name, the rename reaches the
document element — something a JSON/YAML root can never do.

Input:

```xml
<order><customer>Ada</customer></order>
```

Script:

```xml
<rename path="/order" name="opdracht"/>
```

Result:

```xml
<opdracht><customer>Ada</customer></opdracht>
```

Verified by: `TLio.Xml.Tests/Fixtures/XmlRename/02-rename-root/fixture.xml` (also exercised
generically through `XmlFixtureTests.Rename`, and directly via
`XmlRenameTests.Rename_TheDocumentElement_RenamesTheRoot` /
`Rename_TheDocumentElement_KeepsAttributesAndChildren` /
`Rename_TheDocumentElement_LeavesTheCallersReferenceLive` in `TLio.Xml.Tests/Commands/XmlRenameTests.cs`)

## C# Fluent API

```csharp
var script = new TLioScript<XElement>()
    .Rename("opdracht").OnPath("/order");
```

## When to use

- Renaming an XML element, **including the document element** — nothing else can do this.
- Renaming a field while keeping its position among its siblings.
- Renaming every match of a recursive-descent path in one step (`//item`, `$..town`).
- Renaming a container without disturbing its children.

## When NOT to use

- You want to move a node somewhere else in the tree — use `move`.
- You want to change a node's *value* — use `set` (or `put` if it may be absent).
- You want the new name computed from data — `name` is a literal; use `copy` with an
  `=indirect(...)` destination, then `remove`.
- The target is a JSON/YAML root or an array element — it has no name to change.

## Comparison: Rename vs Move vs Copy + Remove

| | `rename` | `move` | `copy` + `remove` |
|---|---|---|---|
| Renames the XML document element | ✅ | ❌ (no destination to express) | ❌ (root has no parent) |
| Keeps position among siblings | ✅ | ❌ appended at destination | ❌ appended |
| Keeps XML attributes | ✅ | ❌ | ❌ |
| Moves a node to a different parent | ❌ | ✅ | ✅ |
| Merges into an existing destination | ❌ | ✅ | ✅ |
| Steps needed | 1 | 1 | 2 |

Use `move` when the node changes *place*; use `rename` when it changes *name*.

## Common mistakes

- **Reaching for `copy` + `remove` to rename.** That appends the copy at the end, losing the
  original position, and it drops XML attributes. Use `rename`.
- **Trying to rename a JSON root.** JSON roots have no name — only XML document elements do.
  Restructure with `move` to `$` if you need different top-level shape.
- **Expecting `name` to accept a function.** It is a literal string.
- **Renaming to a name that is already taken.** In JSON and YAML the existing key is
  overwritten by the rebuilt mapping; check for collisions first with `ifElse`.
- **Assuming a missing path fails loudly.** It logs a warning and continues, like every
  other command.

## Failure modes and what the trace tells you

| Trace message | What it means | Action |
|---------------|---------------|--------|
| `"path X matched 0 nodes; nothing renamed"` | Noop — document unchanged, warning logged | Verify the path; in XML remember the document element is part of it (`/order/customer`, not `/customer`) |
| `"matched N node(s) ... but none could be renamed"` | The nodes have no name of their own — a JSON/YAML root, or array elements | Nothing to rename here; restructure instead |
| `"renamed N node(s) at X to Y"` | Success | None |
| `"has no name to change, or 'Y' is not a usable name"` | The new name is not a legal element name (XML), or the node is unnamed | Check the new name for spaces or punctuation |
| `"Name property for rename command is missing"` | Validation failure — the step did not run | Add the `name` option |

## See Also

[Move.md](Move.md) — relocation, and the one-step rename-within-a-parent idiom.
[../adapters/xml-xpath.md](../adapters/xml-xpath.md) — why `/order/customer` names the document element.
