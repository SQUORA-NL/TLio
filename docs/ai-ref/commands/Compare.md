# compare

> Compares two nodes and writes a result string (`"equal"`, `"greater"`, `"less"`, or
> `"different"`) to a target path.

## Syntax

```json
{ "command": "compare", "fromPath": "$.a", "toPath": "$.b", "resultPath": "$.result" }
```

> See [Notation Reference](../notation-reference.md) for quoting rules and escape sequences.

## Options

| Option | Type | Required | Default | Description |
|--------|------|----------|---------|-------------|
| fromPath | string | yes | — | Path to the first node (left-hand side of comparison). Alias: `firstPath`. |
| toPath | string | yes | — | Path to the second node (right-hand side). Alias: `secondPath`. |
| resultPath | string | yes | — | Path where the result string is written (upsert). |

**Supports functions**: ❌

## Result values

| Value | Meaning |
|-------|---------|
| `"equal"` | Both nodes have equal scalar values |
| `"greater"` | First node's value > second node's value |
| `"less"` | First node's value < second node's value |
| `"different"` | Nodes differ and cannot be ordered (type mismatch, objects, arrays) |

## Formats

Works with all adapters. Path syntax differs per adapter — see [overview.md](../overview.md).

## Example

```json
{ "command": "compare", "fromPath": "$.score", "toPath": "$.threshold", "resultPath": "$.verdict" }
```

## C# Fluent API

```csharp
var script = new TLioScript<JToken>()
    .Compare().From("$.score").To("$.threshold").Result("$.verdict");
```

## When to use

- You need to produce a comparison label (`equal`, `greater`, `less`, `different`) and store it for later use or inspection.
- Validation pipelines where the comparison result is itself an output (e.g., audit logs, assertion records).
- As a preparatory step before an `ifElse` that branches on the comparison result — run `compare` first to write the label, then `ifElse` on `"=fetch($.verdict)"`.
- Comparing two computed or fetched scalar values when you want the label in the document, not just as a runtime condition.

## When NOT to use

- You want to branch based on a comparison result without storing the label — use `ifElse` directly with a function condition instead.
- You want to transform or route data based on a comparison — `compare` only classifies; use `ifElse` or `decisionTable` for the actual transformation after `compare` writes its label.
- You need to compare objects or arrays structurally and act on specific field differences — `compare` returns `"different"` for non-scalar types; it does not diff sub-fields.
- The comparison result is never needed in the output document — if you only need the label as an intermediate runtime value, consider whether `ifElse` with a function condition is simpler.

## Comparison: IfElse vs DecisionTable vs Compare

| Criterion | ifElse | decisionTable | compare |
|-----------|--------|---------------|---------|
| Number of outcomes | 2 | 3+ (or 2 if rules evolve) | 1 label (equal/greater/less/different) |
| Condition complexity | Single expression | Multiple input columns, multi-condition rules | Fixed: left node vs right node |
| Data transformation | Yes — writes any value | Yes — writes rule result values | No — only writes a classification string |
| Rule maintenance | Rewrite JSON | Add/edit a rule row | N/A |
| Overlapping rules | Not supported | bestMatch / allMatches | N/A |
| Use to branch on comparison | Yes — after `compare` writes result | Rarely | No |

## Common mistakes

- **Treating `"different"` as "not equal".**
  `"different"` means the nodes are of incomparable types (e.g., a string vs a number, or an object vs a scalar). Two numbers that are not equal produce `"greater"` or `"less"`, not `"different"`.

- **Expecting `compare` to modify source data.**
  `compare` is read-only on `fromPath` and `toPath`. It writes only to `resultPath`. The original nodes are never changed.

- **Using `compare` alone to branch.**
  `compare` does not branch. To act on the result, follow it with `ifElse`:
  ```json
  [
    { "command": "compare", "fromPath": "$.score", "toPath": "$.threshold", "resultPath": "$.verdict" },
    { "command": "ifElse", "condition": "=fetch($.verdict) == 'greater'", "ifScript": [...], "elseScript": [...] }
  ]
  ```

- **`"equal"` is value equality, not reference equality.**
  Two separate nodes with identical scalar values produce `"equal"`. Object/array nodes always produce `"different"` regardless of content.

- **Comparing nodes that do not exist.**
  If `fromPath` or `toPath` resolves to no node, the comparison cannot be performed. Check that both paths resolve to existing nodes before comparing.

- **Using `compare` for 3-way branching without reading the label.**
  Write the result to `resultPath`, then use `decisionTable` or nested `ifElse` to act on `"equal"`, `"greater"`, `"less"`, and `"different"` as four distinct cases.

## Failure modes and what the trace tells you

- **Trace: `"compared X with Y: result=equal"`** — both nodes resolved to the same scalar value.
- **Trace: `"compared X with Y: result=greater"`** — `fromPath` node is numerically/lexically greater than `toPath` node.
- **Trace: `"compared X with Y: result=less"`** — `fromPath` node is numerically/lexically less than `toPath` node.
- **Trace: `"compared X with Y: result=different"`** — types are incompatible (e.g., string vs number) or nodes are non-scalar (objects, arrays). Not the same as "not equal".
- **No trace / result path unchanged** — one or both paths did not resolve to a node. Verify `fromPath` and `toPath` point to existing nodes in the current document.
- **Unexpected `"different"` result** — check that both nodes are the same scalar type. A string `"42"` and a number `42` are different types and will produce `"different"`.
