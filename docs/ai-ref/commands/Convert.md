# convert

> Changes the format of the whole working document partway through a script. Everything before
> it runs against the old format, everything after against the new one. It is the only command
> the engine cannot run on its own — a format change means a different node type, and a script
> engine works in one node type from start to finish — so it needs
> `MultiFormatScriptRunner`, which splits the script at each boundary and re-hosts each piece.
> Run inline without the runner it converts nothing and reports failure.

## Syntax

```json
{ "command": "convert", "to": "yaml" }
```

> See [Notation Reference](../notation-reference.md) for quoting rules and escape sequences.

`convert` is a command like any other in all three notations — the XML spelling is
`<convert to="json"><settings><inferTypes>true</inferTypes></settings></convert>`, the YAML
spelling a `- command: convert` mapping. The notation a script is written in stays independent of
the format of the data, boundaries included; only the *paths* change at a boundary, because those
follow the document.

## Options

| Option | Type | Required | Default | Description |
|--------|------|----------|---------|-------------|
| to | string | yes | — | Target format id: `json`, `xml`, `yaml`, or any registered adapter. |
| settings | object | no | all defaults | Per-boundary adapter options — see below. |

**Functions in the value**: — `to` is a literal
**Functions in the path**: — this command has no path; it takes the whole document

## Settings

Each exists because the formats genuinely disagree, not because there was no time to pick.

| Setting | Default | What it decides |
|---------|---------|-----------------|
| `attributePrefix` | `"@"` | How an XML attribute is spelled elsewhere. `{"@id": "7"}`. |
| `textProperty` | `"#text"` | The key an element's own value takes when it *also* has attributes: `<price currency="EUR">9.99</price>` is `{"@currency":"EUR","#text":"9.99"}`. |
| `namespacePrefix` | `"xmlns:"` | The key a namespace declaration takes. The XML attribute is always the literal `xmlns:`. |
| `arrayItemName` | `"item"` | What an array item with no name of its own is called in XML. |
| `arrayHandling` | `"wrapped"` | `wrapped` is the canonical shape — one element, items inside. `repeated` is the legacy shape and cannot be addressed reliably; see below. |
| `nullRepresentation` | `"empty"` | `empty` writes `<k/>`; `xsiNil` writes `<k xsi:nil="true"/>`. `xsi:nil` is honoured on read either way. |
| `nameSanitization` | `"sanitize"` | A name XML cannot spell is rewritten quietly (`sanitize`), refused (`error`), or encoded reversibly (`escape`). |
| `inferTypes` | `false` | XML and YAML text carries no declared type; whether `42` becomes a number is a policy call. |
| `cdataAsText` | `false` | Treat CDATA as plain text and drop the marker. |
| `flattenAnchors` | `true` | Dereference YAML anchors and aliases inline. |

Settings apply to **that boundary only**. They do not carry to the next `convert`.

## Formats

Conversion output follows [document-shape.md](../adapters/document-shape.md), so the commands
after a boundary address the tree that page describes:

| JSON | XML | YAML |
|---|---|---|
| `{"k": "v"}` | `<k>v</k>` | `k: v` |
| `{"k": [1,2]}` | `<k><item>1</item><item>2</item></k>` | `k:` + `- 1` / `- 2` |
| `{"k": null}` | `<k/>` | `k: null` |

`arrayHandling: "repeated"` writes `<k>1</k><k>2</k>` instead. It exists for schemas that
insist on it, and it is not the default because in that shape the *parent* element is what
reads as the array — so a path after the boundary lands somewhere else.

## Example

```json
[
  { "command": "add",     "path": "$.order.status", "value": "new"      },
  { "command": "convert", "to": "yaml" },
  { "command": "add",     "path": "order.source",   "value": "portal"   },
  { "command": "convert", "to": "xml"  },
  { "command": "rename",  "path": "/order",         "name":  "opdracht" }
]
```

`{"order":{"id":"7"}}` becomes
`<opdracht><id>7</id><status>new</status><source>portal</source></opdracht>`.

Note the path language changing at each boundary: JSONPath, then YAML dot-notation, then
XPath. That is inherent — the paths address the document, and the document has changed format.

## C# setup

```csharp
var converter = new FormatConverter.Core.FormatConverter();
converter.Register(new JsonFormatAdapter());
converter.Register(new XmlFormatAdapter());
converter.Register(new YamlFormatAdapter());

var runner = new MultiFormatScriptRunner(converter);
runner.RegisterExecutor(new ScriptEngineSectionExecutor<JToken>(
    "json", jsonEngine, JsonExecutionContext.CreateDefault));
runner.RegisterExecutor(new ScriptEngineSectionExecutor<XElement>(
    "xml", xmlEngine, XmlExecutionContext.CreateWithNativeXPath));
runner.RegisterExecutor(new ScriptEngineSectionExecutor<YamlNode>(
    "yaml", yamlEngine, YamlExecutionContext.CreateDefault));

var result = runner.Run("json", document, script);
// result.Document, result.FormatId, result.Success, result.Logs
```

A conversion-only script needs no executors at all.

## When to use

- The document has to leave in a different format than it arrived in.
- Part of the work is easier in another format — XPath predicates over a JSON payload, say.
- A pipeline hands its output to a system that speaks something else.

## When NOT to use

- **One value inside the document is in another format** — use [convertValue](ConvertValue.md).
  That is the common case: a JSON envelope carrying an XML payload as a string.
- You only want the output serialised differently — convert once at the end, or just let the
  caller serialise.
- You are running on a plain `ScriptEngine` with no runner. It will fail, by design.

## Common mistakes

- **Running it without `MultiFormatScriptRunner`.** The engine cannot change node type mid-script.
  It fails and the log says so.
- **Keeping the old path language after the boundary.** `$.order.id` does not select anything
  in XML; it is `/order/id`.
- **Forgetting the notation on the section engines.** The boundary split reads the script in
  whichever of the three notations it is written in and hands each section back in that same
  notation — so an XML script yields XML sections. An engine behind a section executor that was
  never given `UseXmlScripts()` / `UseYamlScripts()` reads such a section as an empty script; the
  executor reports that rather than passing the document through as a success.
- **Expecting settings to persist.** They apply to one boundary.
- **Expecting an empty container to survive.** `<k/>` is equally `null`, `""`, `{}` and `[]`;
  it reads back as null. Nothing else about the document is lossy this way.
- **Expecting an array's item names to survive a trip through JSON.** An array of `<order>`
  comes back as an array of `<item>` — JSON has nowhere to keep the name. XML to XML keeps it.

## Failure modes and what the trace tells you

| Message | What it means | Action |
|---------|---------------|--------|
| `"'convert' to 'X' did nothing…"` | Executed on a bare engine | Run the script through `MultiFormatScriptRunner` |
| `FormatNotRegisteredException` | No adapter for `to` | Register the adapter on the converter |
| `SectionExecutorNotRegisteredException` | A section has commands and no engine to run them | `RegisterExecutor` for that format |
| `FormatParseException` | The document did not parse in the source format | Check the document, and the format the run started in |

## See Also

[ConvertValue.md](ConvertValue.md) — converting one value in place, on the ordinary engine.
[../adapters/document-shape.md](../adapters/document-shape.md) — the shape conversion output takes.
