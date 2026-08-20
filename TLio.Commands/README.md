# TLio.Commands

The built-in command set for [TLio](https://github.com/SQUORA-NL/TLio). Format-agnostic — every
format-specific operation is delegated through `IExecutionContext<TNode>`, so the same commands run
against JSON, XML, and YAML unchanged.

```sh
dotnet add package TLio.Commands
```

## Commands

| Command | Purpose |
|---|---|
| `add` | Add a value, keeping any existing value |
| `set` | Set a value on an existing target |
| `put` | Set a value, creating the target if it is missing |
| `remove` | Delete the targeted item(s) |
| `rename` | Rename a property or element in place |
| `copy` | Copy from one path to another |
| `move` | Move from one path to another |
| `compare` | Structural diff between two paths |
| `merge` | Merge two structures (including key-based array merge) |
| `ifElse` | Conditional branching inside a script |
| `decisionTable` | Table-driven branching |

## Usage

Commands are registered for you by `ParseOptions<TNode>.CreateDefault()` in `TLio.Client`:

```csharp
var options = ParseOptions<JToken>.CreateDefault();
var engine  = new ScriptEngine<JToken>(options.CommandsProvider, options.FunctionsProvider);
```

```json
[
  { "command": "copy",   "from": "$.a", "to": "$.b" },
  { "command": "remove", "path": "$.a" }
]
```

ETL commands (`flatten`, `restore`, `resolve`, `tocsv`) live in `TLio.Extensions.ETL`.

## License

MIT — see the [repository](https://github.com/SQUORA-NL/TLio).
