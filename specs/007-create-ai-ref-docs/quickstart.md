# Quickstart: Using docs/ai-ref/ to Write TLio Scripts

## For AI agents

1. Read `docs/ai-ref/overview.md` first — it gives adapter selection guidance and
   the JSONPath compatibility table.
2. Open the command file(s) you need in `docs/ai-ref/commands/`.
3. If your script uses function values, open the relevant file(s) in
   `docs/ai-ref/functions/`.
4. Write a JSON array of command objects matching the Syntax shown in each file.

## Minimal example

```json
[
  { "command": "set", "path": "$.address.city", "value": "Amsterdam" },
  { "command": "add", "path": "$.tags", "value": ["new"] },
  { "command": "remove", "path": "$.tempField" }
]
```

## Using functions as values

Any `"value"` field accepts a function call string:

```json
{ "command": "set", "path": "$.target", "value": "=fetch($.source)" }
```

## Choosing an adapter

| You have... | Use adapter |
|-------------|-------------|
| JSON + Newtonsoft.Json dependency OK | `TLio.Json` |
| JSON + strict RFC 9535 required | `TLio.Json.SystemText` |
| XML + simple `/root/child` paths | `TLio.Xml` + `CreateWithSlashPaths()` |
| XML + XPath predicates `[@id='1']` | `TLio.Xml` + `CreateWithNativeXPath()` |
| YAML | `TLio.Yaml` |

Full details: `docs/ai-ref/overview.md`.

## Enabling ETL commands

ETL commands (`flatten`, `restore`, `resolve`, `tocsv`) require additional registration:

```csharp
var options = ParseOptions<JToken>.CreateDefault();
options.CommandsProvider.RegisterETL<JToken>();
```

## Validation

After creating ai-ref files, run the compliance check from constitution Article XI:

```pwsh
Get-ChildItem TLio.Commands -Filter "*Command.cs" -Recurse | ForEach-Object {
  $ref = "docs/ai-ref/commands/$($_.BaseName).md"
  if (-not (Test-Path $ref)) { "MISSING: $ref" }
}
Get-ChildItem TLio.Functions -Filter "*Function.cs" -Recurse | ForEach-Object {
  $ref = "docs/ai-ref/functions/$($_.BaseName).md"
  if (-not (Test-Path $ref)) { "MISSING: $ref" }
}
```

Both commands must return zero output.
