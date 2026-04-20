# =partial()

> Selects one element from a **multi-match path expression** by zero-based index. Use
> when a wildcard or recursive path returns multiple nodes and you need a specific one.

## Syntax

```
=partial(<path>)
=partial(<path>, <index>)
```

> See [Notation Reference](../notation-reference.md) for quoting rules and escape sequences.

Used as a value in any command: `"value": "=partial($.items[*])"`

## Arguments

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | string (path) | yes | Path expression expected to match multiple nodes (e.g., `$.items[*]`). |
| 2 | integer | no | Zero-based index of the result to return. Defaults to `0` (first match). |

## Returns

The node at the specified index from the match list. Returns null if the index is
out of range.

## Example

Given `{ "items": ["first", "second", "third"] }`:

```json
{ "command": "set", "path": "$.pick", "value": "=partial($.items[*])" }
```

Result: `$.pick` = `"first"`

```json
{ "command": "set", "path": "$.pick", "value": "=partial($.items[*], 2)" }
```

Result: `$.pick` = `"third"`

## C# Usage

```csharp
// Already registered via ParseOptions.CreateDefault()
var options = ParseOptions<JToken>.CreateDefault();
var engine = new ScriptEngine<JToken>(options.CommandsProvider, options.FunctionsProvider);
var result = engine.Execute(
    "[{\"command\":\"set\",\"path\":\"$.pick\",\"value\":\"=partial($.items[*],1)\"}]",
    JObject.Parse("{\"items\":[\"first\",\"second\",\"third\"]}"),
    JsonExecutionContext.CreateDefault());
```
