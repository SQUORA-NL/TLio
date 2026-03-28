# Quickstart: Sample Projects — API and CLI

**Branch**: `005-samples-api-cli` | **Date**: 2026-03-28

---

## Prerequisites

- .NET 10 SDK installed
- Repository cloned and built: `dotnet build TLio.sln`

---

## Running the API Sample

```sh
# From repository root
dotnet run --project samples/TLio.Sample.Api
```

The API starts on `http://localhost:5100`.

### Try it: JSON transformation

```sh
curl -s -X POST http://localhost:5100/transform/json \
     -H "Content-Type: application/json" \
     -d '{"name": "Alice", "age": 30}'
```

Expected response (200 OK, `Content-Type: application/json`):
```json
{"name":"Alice","age":30,"greeting":"Hello from TLio!"}
```

### Try it: XML transformation

```sh
curl -s -X POST http://localhost:5100/transform/xml \
     -H "Content-Type: application/xml" \
     -d '<person><name>Alice</name></person>'
```

Expected response (200 OK, `Content-Type: application/xml`):
```xml
<person><name>Alice</name><greeting>Hello from TLio!</greeting></person>
```

### Try it: YAML transformation

```sh
curl -s -X POST http://localhost:5100/transform/yaml \
     -H "Content-Type: text/yaml" \
     -d 'name: Alice'
```

Expected response (200 OK, `Content-Type: text/yaml`):
```yaml
name: Alice
greeting: Hello from TLio!
```

### Error cases

```sh
# 400 — empty body
curl -s -o /dev/null -w "%{http_code}" -X POST http://localhost:5100/transform/json \
     -H "Content-Type: application/json" -d ''

# 422 — valid input but script fails (e.g., path not found)
# (depends on the bundled script's behaviour)
```

---

## Running the CLI Sample

```sh
# From repository root — transform the bundled JSON sample input
dotnet run --project samples/TLio.Sample.Cli -- \
  --input samples/TLio.Sample.Cli/SampleInput/sample.json \
  --script samples/TLio.Sample.Cli/Scripts/transform-json.json
```

Expected output (stdout):
```json
{"name":"Alice","age":30,"greeting":"Hello from TLio!"}
```

### Save output to a file

```sh
dotnet run --project samples/TLio.Sample.Cli -- \
  --input samples/TLio.Sample.Cli/SampleInput/sample.json \
  --script samples/TLio.Sample.Cli/Scripts/transform-json.json \
  --output result.json

cat result.json
```

### XML example

```sh
dotnet run --project samples/TLio.Sample.Cli -- \
  --input samples/TLio.Sample.Cli/SampleInput/sample.xml \
  --script samples/TLio.Sample.Cli/Scripts/transform-xml.json
```

### Error example — missing input file

```sh
dotnet run --project samples/TLio.Sample.Cli -- \
  --input nonexistent.json \
  --script samples/TLio.Sample.Cli/Scripts/transform-json.json

echo "Exit code: $?"   # Prints: Exit code: 1
```

---

## What the Bundled Scripts Do

Each bundled script adds a `greeting` property with a static value to the input document.

| Script file | Operation |
|---|---|
| `transform-json.json` | Adds `$.greeting = "Hello from TLio!"` |
| `transform-xml.json`  | Adds `<greeting>Hello from TLio!</greeting>` as a child of the root element |
| `transform-yaml.json` | Adds `greeting: Hello from TLio!` |

---

## Adding Your Own Script

1. Write a TLio script as a JSON array of command objects (see `TLio.UnitTests/Fixtures/` for examples).
2. Save it as `my-script.json`.
3. CLI: `dotnet run --project samples/TLio.Sample.Cli -- --input my-data.json --script my-script.json`
4. API: modify `Scripts/transform-json.json` (or add a new route in `Program.cs`) and restart.
