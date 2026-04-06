# newGuid

> Generates a new random UUID string each time it is evaluated.

## Syntax

```
=newGuid()
```

## Options

None. Takes no arguments.

## Formats

Works with all adapters (returns a string node via `NodeAdapter.CreateString`).

## Example

```json
{ "command": "add", "path": "$.id", "value": "=newGuid()" }
```

Input: `{}`
Output: `{ "id": "a1b2c3d4-e5f6-7890-abcd-ef1234567890" }` (any valid UUID v4)

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
