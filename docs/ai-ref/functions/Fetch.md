# =fetch()

> Evaluates a path expression and returns the **first matched node's value**. Returns an
> optional default value when the path matches nothing.

## Syntax

```
=fetch(<path>)
=fetch(<path>, <defaultValue>)
```

Used as a value in any command: `"value": "=fetch($.source)"`

> See [Notation Reference](../notation-reference.md) for quoting rules.
> `<path>` is an unquoted path argument. `<defaultValue>` is a single-quoted literal or path, e.g. `'Unknown'`.

## Arguments

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | string (path) | yes | Path expression selecting the source node. |
| 2 | any | no | Default value returned when the path resolves to nothing. |

## Returns

The value of the first matched node, or the default value if no match and a default is provided.
Logs a warning and returns failed when the path matches nothing and no default is given.

## Example

```json
{ "command": "set", "path": "$.target", "value": "=fetch($.source)" }
```

```json
{ "command": "add", "path": "$.name", "value": "=fetch($.user.name,'Anonymous')" }
```

## C# Usage

```csharp
// Register via ParseOptions (already included in CreateDefault)
var options = ParseOptions<JToken>.CreateDefault();
// Use in script JSON: "=fetch($.path,'default')"
```

## When to use

- Copying a single scalar or node value from one path into a value expression inline — `"value": "=fetch($.source.price)"`.
- Using the value of one field as the value of another without a standalone `copy` command: e.g. `set` a target to the result of a path expression.
- Providing a safe fallback when a field may be absent: `=fetch($.optionalField,'N/A')`.
- Reading a nested value that must feed into a larger expression — `fetch` composes naturally inside `format`, `concat`, and similar functions when those functions accept path arguments.

## When NOT to use

- You need to transfer an entire object or array as a subtree — use the `copy` command instead. `copy` is a standalone command that moves entire nodes; `fetch` is a value function and returns only the first match.
- You need **all** values from a wildcard path (e.g. `$.items[*].name`) — `fetch` returns only the **first** match. Use `partial` for a specific index or iterate with the path directly.
- The operation is a pure node move with no inline transformation — `copy` is more explicit and readable for whole-node transfers.

## Comparison

| Tool | Kind | Returns | Use when |
|------|------|---------|----------|
| `=fetch()` | Function (inline value) | First matched value | You need a value inline inside `set`/`add`/`put` |
| `copy` command | Standalone command | Entire matched node | You want to duplicate a whole object/array subtree |
| `=partial()` | Function | Nth match from multi-match | You need a specific element from a wildcard result |

## Common mistakes

- **Expecting multiple results**: `fetch` always returns the **first** match only. A wildcard path like `$.items[*].id` resolves to the id of the first item, not a list. Use `partial` with an explicit index if you need a specific element.
- **Using fetch for whole-node copies**: when you intend to copy an entire object tree, prefer the `copy` command — it is cleaner and handles nested structures more explicitly.
- **Missing default on optional paths**: omitting the default when a path may not exist causes a warning and a failed result. Always supply a default for nullable or optional fields.
- **Confusing fetch with indirect**: `fetch` evaluates the path you write literally; `indirect` first reads a string value at that path and then resolves *that string* as a second path.
