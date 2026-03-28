# CLI Contract: TLio.Sample.Cli

**Version**: 1.0 | **Date**: 2026-03-28

## Invocation

```
dotnet run --project samples/TLio.Sample.Cli -- [options]
```

Or after publish:

```
TLio.Sample.Cli [options]
```

## Arguments

| Argument | Required | Description |
|---|---|---|
| `--input <path>` | Yes | Path to the input data file (.json, .xml, .yaml, .yml) |
| `--script <path>` | Yes | Path to the TLio script file (.json) |
| `--output <path>` | No | Path to write the transformed output (default: stdout) |
| `--help` | No | Display usage information and exit |

## Format Detection

The data format is determined from the `--input` file extension:

| Extension | Format | Adapter used |
|---|---|---|
| `.json` | JSON | `JsonExecutionContext.CreateDefault()` |
| `.xml` | XML | `XmlExecutionContext.CreateWithNativeXPath()` |
| `.yaml`, `.yml` | YAML | `YamlExecutionContext.CreateDefault()` |

## Exit Codes

| Code | Meaning |
|---|---|
| 0 | Transformation completed successfully |
| 1 | Input file not found |
| 2 | Script file not found |
| 3 | Input file could not be parsed (format error) |
| 4 | Script file could not be parsed (invalid TLio script JSON) |
| 5 | Transformation execution failed (engine reported failure) |
| 10 | Unknown/unexpected error |

## Output

- On success: transformed document written to stdout (or `--output` file), no other output
- On failure: error description written to **stderr**, nothing written to stdout (or output file)

## Example Invocations

```sh
# Transform a JSON file using a bundled script, print result to stdout
dotnet run --project samples/TLio.Sample.Cli -- --input samples/TLio.Sample.Cli/SampleInput/sample.json --script samples/TLio.Sample.Cli/Scripts/transform-json.json

# Transform an XML file and save result
dotnet run --project samples/TLio.Sample.Cli -- --input sample.xml --script my-script.json --output result.xml

# Display help
dotnet run --project samples/TLio.Sample.Cli -- --help
```
