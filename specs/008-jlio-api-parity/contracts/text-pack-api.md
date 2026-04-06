# Contract: Text Extension Pack API

**Project**: `TLio.Extensions.Text` | **Date**: 2026-04-06

---

## Registration

```csharp
// Extension method on IFunctionsProviderRegistrar<TNode>
public static IFunctionsProviderRegistrar<TNode> RegisterTextPack<TNode>(
    this IFunctionsProviderRegistrar<TNode> registrar)
```

Registers all 12 text functions. Call on `ParseOptions<TNode>.FunctionsProvider`:

```csharp
ParseOptions<JToken>.CreateDefault().FunctionsProvider.RegisterTextPack<JToken>();
```

---

## Function Signatures

All functions follow `=functionName(arg1, arg2, ...)` syntax in script values.

| Function | Signature | Behaviour |
|---|---|---|
| `concat` | `=concat(a, b, ...)` | Concatenates all argument string values |
| `toString` | `=toString(node)` | Returns the string representation of any node |
| `parse` | `=parse(strNode)` | Parses a string into a node via `NodeAdapter.Parse` |
| `format` | `=format(template, value)` | Replaces `{0}` in template with value string |
| `length` | `=length(strNode)` | Returns character count as number node |
| `substring` | `=substring(str, start)` or `=substring(str, start, count)` | Returns substring |
| `replace` | `=replace(str, old, new)` | Replaces all occurrences of old with new |
| `toLower` | `=toLower(str)` | Lowercases the string |
| `toUpper` | `=toUpper(str)` | Uppercases the string |
| `trim` | `=trim(str)` | Trims whitespace from both ends |
| `trimStart` | `=trimStart(str)` | Trims whitespace from start |
| `trimEnd` | `=trimEnd(str)` | Trims whitespace from end |

---

## Script Examples

```json
[
  { "command": "add", "path": "$.fullName", "value": "=concat($.firstName, ' ', $.lastName)" },
  { "command": "add", "path": "$.emailLower", "value": "=toLower($.email)" },
  { "command": "add", "path": "$.codePrefix", "value": "=substring($.code, 0, 3)" },
  { "command": "add", "path": "$.nameLen", "value": "=length($.name)" },
  { "command": "add", "path": "$.cleaned", "value": "=trim($.rawInput)" },
  { "command": "add", "path": "$.label", "value": "=replace($.key, '_', ' ')" }
]
```

---

## Error Handling

- Missing or null arguments: log `LogWarning`, return `FunctionResult.Failed` (no exception thrown — Article X).
- `length` on non-string node: try `TryGetString` first; if null, log warning and return failed.
- `parse` on non-string node: log warning and return failed.
- `substring` with out-of-range indices: clamp to string bounds (no exception).
