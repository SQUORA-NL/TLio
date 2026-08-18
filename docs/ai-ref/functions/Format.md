# format

> Replaces `{0}`, `{1}`, … placeholders in a template string with the supplied argument values.

## Syntax

```
=format(<template>, <value0>)
=format(<template>, <value0>, <value1>, ...)
```

> See [Notation Reference](../notation-reference.md) for quoting rules and escape sequences.

## Arguments

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | string or path | yes | Template string containing `{0}`, `{1}`, … placeholders. |
| 2+ | any | yes (min 1) | Replacement values, substituted in placeholder order. |

## Returns

A string with all `{N}` placeholders replaced by the corresponding argument values.

## Formats

Works with all adapters. Uses `string.Format` internally, with the **invariant culture** — output
does not change with the machine's locale.

## Format specifiers

Numeric and boolean arguments keep their type, so standard .NET specifiers work:

| Expression | Input | Output |
|------------|-------|--------|
| `=format('{0:F2}', $.price)` | `14.5` | `"14.50"` |
| `=format('{0:00000}', $.id)` | `42` | `"00042"` |
| `=format('{0:N2}', $.total)` | `1234.5` | `"1,234.50"` |
| `=format('{0:P0}', $.rate)` | `0.15` | `"15 %"` |

`F` rounds half to even (`{0:F0}` on `14.5` gives `"14"`). For money-style rounding use
[toFixed](ToFixed.md), which rounds half away from zero.

## Example

```json
{ "command": "add", "path": "$.greeting", "value": "=format('Hello, {0}!', $.name)" }
```

Input: `{ "name": "Alice" }`
Output: `{ "name": "Alice", "greeting": "Hello, Alice!" }`

## C# Usage

```csharp
var options = ParseOptions<JToken>.CreateDefault();
options.FunctionsProvider.RegisterText<JToken>();
var engine = new ScriptEngine<JToken>(options.CommandsProvider, options.FunctionsProvider);
var result = engine.Execute(
    "[{\"command\":\"add\",\"path\":\"$.msg\",\"value\":\"=format('Hi {0}, you are {1}', $.name, $.role)\"}]",
    JObject.Parse("{\"name\":\"Alice\",\"role\":\"admin\"}"),
    JsonExecutionContext.CreateDefault());
```

## When to use

- Producing human-readable sentences or messages where field values are inserted into a fixed template (`"Dear {0}, your order {1} has shipped."`).
- The same value is reused at multiple positions in the template — reference it with the same index (`{0}`) rather than repeating the path.
- You have 2+ values and a prose template — `format` is cleaner than a deeply nested `concat` with many literal args.
- Building log messages, notification bodies, display labels, or any text with a fixed structure and variable slots.

## When NOT to use

- You need named placeholders — `format` only supports positional `{0}`, `{1}`, … placeholders. Use `concat` with literal text between path arguments if named readability matters more.
- You are joining just 2–3 fields with no surrounding prose — `concat` is simpler: `=concat($.first,' ',$.last)` vs `=format('{0} {1}', $.first, $.last)`.
- You need locale-sensitive number/date formatting (e.g., decimal separators, date formats) — `format` uses `string.Format` with `InvariantCulture` semantics; the output format for numbers follows .NET default rules.
- The template is stored in the document and changes at runtime — `format` requires the template as a literal or a path that resolves to a valid template string. Dynamic templates with unknown placeholder counts are fragile.

## Comparison

| Function | Input style | Placeholder type | Use when |
|----------|------------|-----------------|----------|
| `format` | Template + positional args | `{0}`, `{1}`, … | Prose templates with ordered slots |
| `concat` | Fixed individual args | Literal text between args | 2–4 known fields, arbitrary separators |
| `join` | Array path + single separator | N/A | Dynamic list of same-type values |

## Common mistakes

- **Placeholder index must match argument position**: `{0}` maps to the second argument (first after template), `{1}` to the third, etc. Off-by-one errors cause wrong substitutions or runtime failures.
- **Unused placeholders**: if the template contains `{2}` but only two value arguments are supplied, `string.Format` throws a format exception. Count placeholders carefully.
- **Curly braces in literal text**: to output a literal `{` or `}` in the result, use `{{` and `}}` in the template string. A single `{` followed by a non-numeric character may cause a format exception.
- **Path resolution**: arguments resolve against the document root (dataContext). `@.field` inside a function refers to the ROOT, not a parent element.
- **Wildcard paths**: `$.items[*].name` as a value argument passes a flat list, not a single value. Use indexed paths for predictable substitution.
- **Null values**: a null argument is substituted as an empty string in the output, silently dropping that slot.
