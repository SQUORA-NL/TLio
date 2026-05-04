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

## Arguments

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | string (path) | yes | Path expression selecting the source node. Bare (`$.f`) or quoted (`'$.f'`) — both work identically. |
| 2 | any | no | Default value returned when the path resolves to nothing. |

## Invocation styles

Both styles produce identical results at the top level of a command value:

```json
{ "command": "set", "path": "$.target", "value": "=fetch($.source)" }
{ "command": "set", "path": "$.target", "value": "=fetch('$.source')" }
```

The quoted form is particularly useful when `fetch` is used as an argument to another
function, and the path itself contains a dynamic expression.

## Dynamic path computation

When the argument inside quotes begins with `=`, it is evaluated as a function expression
and its string result is used as the path:

```json
{ "command": "set", "path": "$.email", "value": "=fetch('=indirect($.pathField)')" }
```

If `$.pathField` holds the string `"$.customer.email"`, the above fetches
`$.customer.email` at runtime.

Another example using `concat` to build a path from field values:

```json
{ "command": "set", "path": "$.price", "value": "=fetch('=concat($.category, $.priceKey)')" }
```

If `$.category` = `"$.products."` and `$.priceKey` = `"widget.price"`, the path becomes
`$.products.widget.price`.

### Doubled-quote escape inside quoted arguments

To include a literal single-quote inside a quoted argument, double it (`''`):

```
=fetch('=concat(''$.'', $.fieldname, ''.price'')')
```

After the `''` → `'` unescape, the inner expression becomes:
`concat('$.', $.fieldname, '.price')`, which builds a path dynamically.

## Path-detection rule

Fetch detects whether its resolved argument is a path by checking if the string
starts with `$` or `@`. If it does not, the value is returned directly without a
path lookup — this means a non-path result from a nested function is always returned
correctly rather than failing with a silent no-match.

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

```json
{ "command": "set", "path": "$.email", "value": "=fetch('=indirect($.pathRef)')" }
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
- **Dynamic path computation**: when the path itself is stored in a data field, use `=fetch('=indirect($.pathField)')` to resolve it at runtime.

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
| `=indirect()` | Function | Node at a stored path | The path itself is stored in a data field |

## Common mistakes

- **Expecting multiple results**: `fetch` always returns the **first** match only. A wildcard path like `$.items[*].id` resolves to the id of the first item, not a list. Use `partial` with an explicit index if you need a specific element.
- **Using fetch for whole-node copies**: when you intend to copy an entire object tree, prefer the `copy` command — it is cleaner and handles nested structures more explicitly.
- **Missing default on optional paths**: omitting the default when a path may not exist causes a warning and a failed result. Always supply a default for nullable or optional fields.
- **Confusing fetch with indirect**: `fetch` evaluates the path you write literally; `indirect` first reads a string value at that path and then resolves *that string* as a second path. For fully dynamic paths combine them: `=fetch('=indirect($.pathField)')`.
- **Literal `=expr` inside quotes**: to store the string `=expr` literally, use `==` escape: `'==expr'` → literal `=expr`. Without the second `=`, the expression is evaluated as a function call.
