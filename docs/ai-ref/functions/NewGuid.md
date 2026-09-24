# newGuid

> Generates a new random UUID string each time it is evaluated.

## Syntax

```
=newGuid()
```

> See [Notation Reference](../notation-reference.md) for quoting rules and escape sequences.

## Options

None. Takes no arguments.

## Formats

Works with all adapters (returns a string node via `NodeAdapter.CreateString`).

## Verified example

```json
{ "command": "add", "path": "$.id", "value": "=newGuid()" }
```

`newGuid()` is random, so no fixture asserts a literal value; the inline NUnit tests assert
the two properties that matter instead:

- The result parses as a GUID (`Guid.TryParse` succeeds) — verified by
  `NewGuidTests.NewGuid_ReturnsString`
  (`TLio.Functions.Tests/FunctionsTests/TextTests/NewGuidTests.cs:23-31`).
- Two calls produce different values — verified by `NewGuidTests.NewGuid_EachCallIsUnique`
  (same file, lines 33-40).

## Notes

- Also available as `"newguid"` (lowercase) via `TLio.Extensions.Text.RegisterText()`.
- The `ParseOptions.CreateDefault()` version registers as `"newGuid"` (camelCase).
- UUID is generated at script execution time, not at parse time.

## C# Usage

```csharp
var options = ParseOptions<JToken>.CreateDefault();
var engine = new ScriptEngine<JToken>(options.CommandsProvider, options.FunctionsProvider);
var result = engine.Execute(
    "[{\"command\":\"add\",\"path\":\"$.id\",\"value\":\"=newGuid()\"}]",
    JObject.Parse("{}"),
    JsonExecutionContext.CreateDefault());
```

## When to use

- Generating **unique identifiers** for new records being created by a transformation — e.g. adding a primary key to a synthesised object.
- Producing **idempotency keys** or **correlation IDs** to attach to events, messages, or log entries.
- Creating **trace identifiers** that link a set of transformed records to a single script execution.
- Any scenario where uniqueness matters and the ID does not need to be reproducible or human-readable.

## When NOT to use

- You need a **deterministic or reproducible ID** — `newGuid()` is random every evaluation. Use a hash, a composite key from existing fields, or an external ID source instead.
- You need **sequential or ordered IDs** — GUIDs are unordered. Use an external counter or database sequence.
- The same ID must appear in **multiple places in the same script run** — each invocation of `newGuid()` produces a different value. Compute the ID once, store it at a known path, and reference that path for subsequent uses.

## Comparison

| Approach | Deterministic? | Use when |
|----------|---------------|----------|
| `=newGuid()` | No (random each call) | Unique ID needed, reproducibility not required |
| Composite key from fields | Yes | ID derived from existing data |
| External counter / sequence | Yes | Sequential or ordered IDs required |

## Common mistakes

- **Expecting the same ID across multiple steps**: each command that uses `=newGuid()` generates an **independent, different** UUID. If you need the same ID in two places (e.g. a record ID and a foreign key in the same script), generate it once with an `add` command, then `fetch` that path in subsequent steps.
- **Using newGuid for deterministic idempotency**: if your system reprocesses the same input and needs the same output ID, `newGuid()` breaks idempotency. Use a hash of the input fields instead.
- **Case sensitivity of the function name**: the default registration is `"newGuid"` (camelCase). The `TLio.Extensions.Text` pack registers `"newguid"` (lowercase). Use the form that matches your registration. Calling `"newguid"` without the text pack will fail.
