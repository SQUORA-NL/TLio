# path

> Returns the current node's absolute path as a string. Alias for `=scriptpath()`.

## Syntax

```
=path()
```

> See [Notation Reference](../notation-reference.md) for quoting rules and escape sequences.

## Options

None. Identical behaviour to `=scriptpath()`.

## Formats

Works with all adapters. Path format is adapter-specific (e.g., `$.items[0]` for JSON).

## Example

```json
{ "command": "add", "path": "$.items[*].loc", "value": "=path()" }
```

Input: `{ "items": [{ "id": 1 }, { "id": 2 }] }`
Output: `{ "items": [{ "id": 1, "loc": "$.items[0]" }, { "id": 2, "loc": "$.items[1]" }] }`

## Notes

- `=path()` and `=scriptpath()` are registered separately but share the same implementation (`ScriptPath<TNode>`).
- Use `=path()` for JLio compatibility; use `=scriptpath()` for explicit TLio naming.

## C# Usage

```csharp
// Both are identical at runtime:
options.FunctionsProvider.Register("path",       () => new ScriptPath<JToken>());
options.FunctionsProvider.Register("scriptpath", () => new ScriptPath<JToken>());
```
