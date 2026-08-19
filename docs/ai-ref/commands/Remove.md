# remove

> Removes all nodes matched by the path expression. Logs a warning if no nodes match;
> does not error.

## Syntax

```json
{ "command": "remove", "path": "$.fieldToDelete" }
```

> See [Notation Reference](../notation-reference.md) for quoting rules and escape sequences.

## Options

| Option | Type | Required | Default | Description |
|--------|------|----------|---------|-------------|
| path | string | yes | — | Selects the node(s) to remove. Wildcards remove multiple nodes. |

**Functions in the value**: — no value field  
**Functions in the path**: ✅ `=indirect()` in `path`

## Formats

Works with all adapters. Path syntax differs per adapter — see [overview.md](../overview.md).

## Example

```json
[
  { "command": "remove", "path": "$.tempId" },
  { "command": "remove", "path": "$.items[?(@.active == false)]" }
]
```

## C# Fluent API

```csharp
var script = new TLioScript<JToken>()
    .Remove().OnPath("$.tempId");
```

## When to use

- Stripping sensitive fields (PII, credentials) before publishing or persisting a document.
- Cleaning up temporary/staging fields after processing is complete.
- Removing metadata that should not appear in final output.
- Deleting multiple nodes in one step using wildcards (`[*]`) or recursive descent (`..field`).
- Idempotent cleanup: safe to run even if the node was already removed (noop, not failure).

## When NOT to use

- You want to update a value — `remove` only deletes; combine with `add`/`set`/`put` for a replace-with-new-value pattern.
- You want to relocate the node — use `move` (deletes source as part of the operation).
- You want to keep a copy before deleting — use `copy` first, then `remove`; or use `move` in a single step.
- The path selects the document root (`$`) — removing root is not a meaningful operation; restructure with `move` instead.

## Comparison: Copy vs Move

> `remove` is not a structural alternative to `copy` or `move`, but it frequently pairs with them.

| Pattern | Commands | When to use |
|---------|----------|-------------|
| Relocate a node | `move` | Single step — copy + delete atomically |
| Duplicate then delete original | `copy` → `remove` | When intermediate state must be observable |
| Delete only | `remove` | Field is no longer needed at all |
| Replace value | `remove` → `add`/`set` | Value changes, not just location |

## Common mistakes

- **Using remove to "update" a value.** `remove` deletes the node entirely. To change its value, use `set`/`put`/`add` directly — no need to remove first.
- **Treating a noop (no match) as confirmation of deletion.** A noop trace means the path matched nothing — either the field was already absent, or the path expression is wrong. Verify the path if deletion was expected.
- **Overly broad wildcard deleting more nodes than intended.** Paths like `$.items[*]` remove every element. Test path expressions against sample data before running in production.
- **Removing a node that a later step still reads.** Steps after `remove` will find nothing at that path. Reorder steps or use a copy to preserve the value if it is needed downstream.
- **Assuming remove fails loudly on a missing path.** It does not — it logs a warning and continues. Scripts will not throw on missing paths; check the trace to confirm actual deletions.

## Failure modes and what the trace tells you

| Trace message | What it means | Action |
|---------------|---------------|--------|
| `"no nodes matched path X"` | Path matched nothing — noop, document unchanged, warning logged | Not a failure; verify path if deletion was expected |
| `"removed N node(s) at X"` | Success — N nodes deleted from document | None |
| No trace entry for this step | Step was skipped (script compilation issue) | Check script JSON for syntax errors |
