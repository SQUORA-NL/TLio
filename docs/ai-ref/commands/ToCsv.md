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
| includeHeaders | boolean | `true` | Emit a header row with field names. |
| includeTypeColumns | boolean | `false` | Include companion `<name><typeColumnSuffix>` columns if present on the source objects (e.g., ones left over from `flatten` with `preserveTypes: true`). |
| typeColumnSuffix | string | `"_type"` | Suffix identifying a type-indicator column to include/exclude, per `includeTypeColumns`. |
| includeMetadata | boolean | `false` | Include columns whose name starts with `_` or contains `"Metadata"` (e.g., a `flatten` metadata property that ended up as a sibling field). |
| quoteAllFields | boolean | `false` | Force quoting of every field, not just fields that contain the delimiter, the quote character, a newline, or leading/trailing spaces. |
| escapeQuoteChar | string | `"\""` | Character used both to quote a field and to escape occurrences of itself within it (doubled, in the usual CSV style). |
| nullValueRepresentation | string | `""` | String used for `null` values, and for a column a given row's object does not have at all. |
| booleanFormat | string | `"true,false"` | **Comma-separated** true/false spelling, e.g. `"1,0"` or `"yes,no"` — not slash-separated as the value might suggest; the command splits on `,`. |
| lineEnding | string | `"\r\n"` | Present on `CsvSettings` but **not read** by the current `tocsv` implementation — rows are always joined with the platform's `Environment.NewLine` (via `StringBuilder.AppendLine`) regardless of this setting. Confirmed by the sweep fixture, which sets `"lineEnding": "|"` and still gets `\n`-joined output. |
| encoding | string | `"UTF-8"` | Present on `CsvSettings`; `tocsv` writes a CSV string into the document, which has no byte encoding of its own — this setting has no effect on that string. It matters only if a caller later serializes the containing document to bytes with a component that consults it, which the built-in adapters do not. |

**Functions in the value**: — no value field  
**Functions in the path**: — not resolved here; resolve it in a preceding step

## Formats

Works with all adapters. Path syntax differs per adapter — see [overview.md](../overview.md).

## Example

```json
{ "command": "tocsv", "path": "$.rows", "csvSettings": { "delimiter": ",", "includeHeaders": true } }
```

## Verified example

An array of two objects becomes a CSV string in place at `$.table`. Columns are sorted
alphabetically (`age` before `name`), and the default line ending is `\r\n` (the test splits on
both `\r` and `\n` to check row count without depending on which).

```json
{
  "input": {
    "table": [
      { "name": "Alice", "age": 30 },
      { "name": "Bob",   "age": 25 }
    ]
  },
  "script": [
    { "command": "tocsv", "path": "$.table" }
  ]
}
```

`data.table` afterward is the single string:

```csv
age,name
30,Alice
25,Bob
```

Verified by:
`TLio.UnitTests/CommandsTests/ETLTests/FlattenRestoreTests.cs::ToCsv_ArrayOfObjects_ProducesAlphabeticHeaders`
(the test asserts the header row equals `"age,name"` and each data row contains its expected
values — the exact field order within a data row is not pinned beyond the shared column order).

For `csvSettings` behavior specifically — delimiter overrides, `includeHeaders: false`, and null
handling — see `TLio.Functions.Tests/FunctionsTests/ETLTests/ToCsvTests.cs`
(`ToCsv_SemicolonDelimiter_UsesSemicolons`, `ToCsv_NoHeaders_ProducesDataWithoutHeaderRow`,
`ToCsv_NullFieldValue_RendersAsEmptyCell`). A cross-format `tocsv` with a custom `delimiter` and
`lineEnding` also runs through JSON, XML and YAML at `$.etl.csv` in
`TLio.Parity.Tests/Sweep/sweep.json` / `sweep.xml` — its recorded output confirms `lineEnding` has
no effect (see the Settings table above).

## When to use

- Final export step: the pipeline has produced a clean, flat array of uniform objects and the consumer needs a CSV string (file output, API response, clipboard).
- The source array has already been normalised so every object has the same set of top-level keys — `tocsv` derives headers from property names and expects consistent structure.
- Configuring delimiter, quoting, or boolean/null representation to match a specific downstream system (e.g., semicolons for European locales, `"1,0"` for boolean flags).

## When NOT to use

- Mid-pipeline as an intermediate format — once data is a CSV string it cannot be queried or transformed by subsequent TLio commands. Place `tocsv` as the last command for a given output path.
- The source objects are nested — a nested object or array property is serialized to a JSON-text cell (via `adapter.Serialize`), producing an unreadable, hard-to-split column. Use `flatten` first to bring all fields to the top level.
- The source contains arrays within objects — inner arrays stringify to a single cell value. Flatten or unroll them before converting.
- You need structured output (JSON, XML, YAML) for a downstream service — use the appropriate format adapter or `copy`/`set` commands instead.

## Common mistakes

- **Nested objects in the source array**: fields that are objects or arrays are serialized to a JSON-text cell (e.g. `{"x":1}`, `["a","b"]`), not a human-readable summary. Always flatten or project to scalar fields before calling `tocsv`.
- **Non-uniform objects**: if array items have different property sets, columns will be misaligned or missing for some rows. Normalise the array first (e.g., add missing fields with `set` + a default value).
- **Placing `tocsv` before further transforms**: subsequent commands that expect JSON nodes cannot operate on a CSV string. Ensure `tocsv` is the terminal command for that data path.
- **Forgetting `includeHeaders`**: the default is `true`, so a header row is always emitted unless explicitly disabled. If the consumer expects raw data rows only, set `"includeHeaders": false`.
- **Delimiter conflicts with data**: if field values contain the delimiter character and `quoteAllFields` is false, only fields that contain the delimiter are auto-quoted. Verify output with a sample row that contains the delimiter in a value.

## Failure modes and what the trace tells you

- **Columns contain embedded JSON text**: source objects have nested properties. Trace will show the raw node passed to `tocsv`; confirm it is fully flat before this command.
- **Row count mismatch**: `path` resolves to a single object rather than an array. `tocsv` emits one row per array element; a single object produces a single row with all its keys as headers. Verify the path selects the array, not a parent wrapper.
- **Output is an empty string**: `path` resolved to an empty array. Check earlier pipeline steps to confirm the array was populated.
- **ETL extension not registered**: trace reports an unknown command `tocsv`. Ensure `RegisterETL<TNode>()` is called on the commands provider at startup.
