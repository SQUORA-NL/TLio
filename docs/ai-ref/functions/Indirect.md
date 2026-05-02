# =indirect()

> Two-step path resolution: reads a **string value** at the given path, then uses that
> string as a second path expression to retrieve the final value.

## Syntax

```
=indirect(<path>)
```

> See [Notation Reference](../notation-reference.md) for quoting rules and escape sequences.

Used as a value in any command: `"value": "=indirect($.pathRef)"`

## Arguments

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | string (path) | yes | Path to a node whose **string content** is a valid path expression. |

## Returns

The value found at the dynamically resolved path. Logs a warning if either path
resolves to nothing.

## Example

Given `{ "pathRef": "$.source", "source": "hello" }`:

```json
{ "command": "set", "path": "$.target", "value": "=indirect($.pathRef)" }
```

Result: `$.target` = `"hello"` (resolved via `$.pathRef` → `"$.source"` → `"hello"`).

## C# Usage

```csharp
// Already registered via ParseOptions.CreateDefault()
var options = ParseOptions<JToken>.CreateDefault();
var engine = new ScriptEngine<JToken>(options.CommandsProvider, options.FunctionsProvider);
var result = engine.Execute(
    "[{\"command\":\"set\",\"path\":\"$.target\",\"value\":\"=indirect($.pathRef)\"}]",
    JObject.Parse("{\"pathRef\":\"$.source\",\"source\":\"hello\"}"),
    JsonExecutionContext.CreateDefault());
```

## When to use

- The path to resolve is **stored in the data itself** — e.g. a `type` field determines which sub-object to read, and a lookup table maps type names to paths.
- Dynamic field selection: a configuration key holds the name/path of the field to operate on, and that key changes per record.
- Configurable routing: the script is generic and the data supplies which field to target — without rewriting the script for each variant.
- Example: `$.config.outputField` contains `"$.users[0].email"` — `=indirect($.config.outputField)` resolves the email without hard-coding the path.

## When NOT to use

- The path is **known at script-write time** — write the path directly (`=fetch($.user.email)`). Using `indirect` adds indirection with no benefit.
- You need multi-hop resolution (the resolved path itself points to another path) — `indirect` resolves exactly **one** level of indirection.
- The referenced string is a field name only (e.g. `"email"`) rather than a full path expression — `indirect` requires a complete, valid path starting with `$` or `@`.

## Comparison

| Function | Resolution | Use when |
|----------|-----------|----------|
| `=fetch()` | Evaluates the path you write literally | Path is known at script time |
| `=indirect()` | Reads a string at arg path; resolves that string as a second path | Path is stored in the data at runtime |

## Common mistakes

- **Storing a field name instead of a full path**: `indirect` needs a fully-qualified path expression (e.g. `"$.user.email"`), not just a field name (`"email"`). A bare name is not a valid path and will fail to resolve.
- **Forgetting the `$` or `@` prefix**: the string value at the first path must be a valid path expression. `"user.email"` without a root prefix will not resolve correctly.
- **Expecting multi-hop indirection**: if the resolved value is itself a path reference, `indirect` does not follow it again. Only one level of dynamic resolution is performed.
- **Confusing with fetch**: `fetch` resolves the path argument directly; `indirect` resolves the path argument to get a *string*, then resolves *that string* as a path. Two hops, not one.
