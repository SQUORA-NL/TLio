# tocsv

> Converts an object or array of objects to a CSV-formatted string and writes it to
> the target node.

> **ETL extension**: requires `options.CommandsProvider.RegisterETL<TNode>()` in addition
> to `ParseOptions<TNode>.CreateDefault()`.

## Syntax

```json
{ "command": "tocsv", "path": "$.records" }
```

With settings:

```json
{ "command": "tocsv", "path": "$.data", "csvSettings": { "delimiter": ";", "includeHeaders": true } }
```

> See [Notation Reference](../notation-reference.md) for quoting rules and escape sequences.

## Options

| Option | Type | Required | Default | Description |
|--------|------|----------|---------|-------------|
| path | string | yes | — | Selects the node(s) to convert (object or array of objects). |
| csvSettings | object | no | — | CSV formatting configuration (see Settings below). |

### Settings object

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| delimiter | string | `","` | Column separator character. |
| includeHeaders | boolean | true | Emit a header row with field names. |
| booleanFormat | string | `"true/false"` | How boolean values are rendered (e.g., `"1/0"`, `"yes/no"`). |
| nullValueRepresentation | string | `""` | String used for null values. |
| quoteAllFields | boolean | false | Force quoting of every field, not just fields with special characters. |
| escapeQuoteChar | string | `"\""` | Character used to escape quotes within field values. |

**Supports functions**: ❌

## Formats

Works with all adapters. Path syntax differs per adapter — see [overview.md](../overview.md).

## Example

```json
{ "command": "tocsv", "path": "$.rows", "csvSettings": { "delimiter": ",", "includeHeaders": true } }
```

## When to use

- Final export step: the pipeline has produced a clean, flat array of uniform objects and the consumer needs a CSV string (file output, API response, clipboard).
- The source array has already been normalised so every object has the same set of top-level keys — `tocsv` derives headers from property names and expects consistent structure.
- Configuring delimiter, quoting, or boolean/null representation to match a specific downstream system (e.g., semicolons for European locales, `1/0` for boolean flags).

## When NOT to use

- Mid-pipeline as an intermediate format — once data is a CSV string it cannot be queried or transformed by subsequent TLio commands. Place `tocsv` as the last command for a given output path.
- The source objects are nested — nested object properties are stringified (e.g., `[object Object]`), producing unreadable columns. Use `flatten` first to bring all fields to the top level.
- The source contains arrays within objects — inner arrays stringify to a single cell value. Flatten or unroll them before converting.
- You need structured output (JSON, XML, YAML) for a downstream service — use the appropriate format adapter or `copy`/`set` commands instead.

## Common mistakes

- **Nested objects in the source array**: fields that are objects or arrays produce `[object Object]` or stringified JSON in the cell. Always flatten or project to scalar fields before calling `tocsv`.
- **Non-uniform objects**: if array items have different property sets, columns will be misaligned or missing for some rows. Normalise the array first (e.g., add missing fields with `set` + a default value).
- **Placing `tocsv` before further transforms**: subsequent commands that expect JSON nodes cannot operate on a CSV string. Ensure `tocsv` is the terminal command for that data path.
- **Forgetting `includeHeaders`**: the default is `true`, so a header row is always emitted unless explicitly disabled. If the consumer expects raw data rows only, set `"includeHeaders": false`.
- **Delimiter conflicts with data**: if field values contain the delimiter character and `quoteAllFields` is false, only fields that contain the delimiter are auto-quoted. Verify output with a sample row that contains the delimiter in a value.

## Failure modes and what the trace tells you

- **Columns contain `[object Object]`**: source objects have nested properties. Trace will show the raw node passed to `tocsv`; confirm it is fully flat before this command.
- **Row count mismatch**: `path` resolves to a single object rather than an array. `tocsv` emits one row per array element; a single object produces a single row with all its keys as headers. Verify the path selects the array, not a parent wrapper.
- **Output is an empty string**: `path` resolved to an empty array. Check earlier pipeline steps to confirm the array was populated.
- **ETL extension not registered**: trace reports an unknown command `tocsv`. Ensure `RegisterETL<TNode>()` is called on the commands provider at startup.
