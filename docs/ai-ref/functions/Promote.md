# =promote()

> Wraps the matched node in a new object using either the node's own **property name**
> or an **explicit name** as the key.

## Syntax

```
=promote(path)
=promote(path, propertyName)
```

Used as a value in any command: `"value": "=promote($.person)"`

## Arguments

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | string (path) | yes | Path to the node to promote. |
| 2 | string | no | Explicit key name for the wrapper object. When omitted, uses the node's parent property name. |

## Returns

An object with one key whose value is the matched node.

## Example

Using parent property name (1-arg):

```json
{ "command": "set", "path": "$.result", "value": "=promote($.person)" }
```

Given `{ "person": { "name": "Alice" } }` → `$.result` = `{ "person": { "name": "Alice" } }`

Using explicit name (2-arg):

```json
{ "command": "add", "path": "$.wrapped", "value": "=promote($.rawValue,'data')" }
```

Given `{ "rawValue": 42 }` → `$.wrapped` = `{ "data": 42 }`

## C# Usage

```csharp
var options = ParseOptions<JToken>.CreateDefault();
// Use in script JSON: "=promote($.path,'wrapperKey')"
```
