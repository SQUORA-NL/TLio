# convertValue

> Converts one value inside the document from one format to another, leaving everything around
> it alone. This is the everyday half of format conversion: a JSON envelope carrying an XML
> payload as a string, or one branch that has to go out to a system speaking something else.
> The document's own format never changes, so this runs on the ordinary engine — no runner.

## Syntax

```json
{ "command": "convertValue", "path": "$.payload", "from": "xml", "to": "json" }
```

> See [Notation Reference](../notation-reference.md) for quoting rules and escape sequences.

## Options

| Option | Type | Required | Default | Description |
|--------|------|----------|---------|-------------|
| path | string | yes | — | Selects the value(s) to convert. Every match is converted. |
| from | string | no | the document's own format | The format the value is *in*. Given, the node is read as text; omitted, the node is structure and is serialised. |
| to | string | yes | — | The format to convert to. |
| settings | object | no | all defaults | Same settings as [convert](Convert.md). |

**Functions in the value**: — `from` and `to` are literals
**Functions in the path**: ✅ standard path selection, including wildcards

## The two directions

`from` is what decides which way this goes.

**Text into structure** — the node holds a string in another format:

```json
{ "command": "convertValue", "path": "$.payload", "from": "xml", "to": "json" }
```

```
{"id":"1","payload":"<order><sku>A1</sku></order>"}
{"id":"1","payload":{"order":{"sku":"A1"}}}
```

**Structure into text** — the node is part of the document and leaves as a string:

```json
{ "command": "convertValue", "path": "$.order", "to": "xml" }
```

```
{"id":"1","order":{"sku":"A1"}}
{"id":"1","order":"<order><sku>A1</sku></order>"}
```

A subtree is serialised together with the name it is known by, so `$.order` becomes
`<order>…</order>` rather than its contents with the name dropped.

## Structure or text

The result is **structure** when `to` names the document's own format, and **text** otherwise.
That is the only available answer: a JSON document has no way to hold XML except as a string.

| Document | `to` | Result at the path |
|---|---|---|
| JSON | `json` | an object or array |
| JSON | `xml` | a string |
| XML | `xml` | element content |
| XML | `json` | element text |

In XML, replacing keeps the element and swaps its content — `<payload>` stays `<payload>`.

## Verified example

```json
[{ "command": "convertValue", "path": "$.messages[*].body", "from": "xml", "to": "json" }]
```

```
{"messages":[{"body":"<m><a>1</a></m>"},{"body":"<m><a>2</a></m>"}]}
{"messages":[{"body":{"m":{"a":"1"}}},{"body":{"m":{"a":"2"}}}]}
```

The wildcard reaches every node the path selects, not just the first — both message bodies are
parsed out of XML into structure in one step, and a following command can then address either
one as ordinary JSON.

Verified by: `TLio.FormatConverter.Tests/Integration/ConvertValueTests.cs` —
`EveryMatchedNodeIsConverted`.

## Performance

`convertValue` runs on the ordinary `ScriptEngine<TNode>`, which has its own precompilation:
`ScriptEngine<TNode>.Compile` parses a script once into a `CompiledScript<TNode>` that can then
execute against many documents without re-parsing — the same parse-once principle
`MultiFormatScriptRunner.Compile` applies on the multi-format side (see
[convert](Convert.md#performance)). The AfdApi sample's numbers make the underlying cost
concrete even though its scripts don't happen to use `convertValue`: parsing a multi-MB script
used to add roughly 130ms to each ~220ms request for `afdshort-to-afd2`, and compiling once at
startup instead of per-request brought steady-state latency for all three conversion directions
down to the 15-70ms range. The fix is the same regardless of which commands a script contains —
compile once, run many times, and warm up the JIT with a real request before serving traffic.

## When to use

- A field holds a document in another format — the classic envelope-with-payload.
- You need to reach *inside* an embedded payload with normal commands.
- One branch has to be handed out as text in another format.

## When NOT to use

- **The whole document changes format** — use [convert](Convert.md) with
  `MultiFormatScriptRunner`.
- You want to reshape data within one format — use `move`, `copy` or `merge`.

## Common mistakes

- **Using `convert` with a `path`.** `convert` has no path and is intercepted by the runner
  before the engine sees it. These are separate commands on purpose: one mistyped `path` would
  otherwise silently convert the entire document.
- **Omitting `from` when the node holds text.** Without `from` the node is treated as structure
  and serialised, so a string is converted as a string.
- **Expecting text output to stay addressable.** Once a subtree becomes a string, paths no
  longer reach into it. Convert it back first.
- **Forgetting to register the adapter** for `from` or `to` on the converter.

## Failure modes and what the trace tells you

| Trace message | What it means | Action |
|---------------|---------------|--------|
| `"path X matched 0 nodes; nothing converted"` | Noop — document unchanged, warning logged | Verify the path |
| `"holds no text to read as F"` | `from` was given but the node is not a scalar | Drop `from`, or point at the string |
| `"could not convert X from F to T: …"` | The value did not parse, or a format is unregistered | Check the payload and the registered adapters |
| `"requires a non-empty 'to' format"` | Validation failure — the step did not run | Add `to` |
| `"converted N of M node(s)"` | Success | None |

## C# setup

Both commands live in the `TLio.FormatConverter` package, which holds the three format
adapters as well:

```sh
dotnet add package TLio.FormatConverter
```

```csharp
var options = ParseOptions<JToken>.CreateDefault();
options.CommandsProvider.RegisterFormatConversion<JToken>(converter, "json");
```

The format id is the format *this engine's documents* are in — it is what decides whether a
converted value comes back as structure or as text.

## See Also

[Convert.md](Convert.md) — changing the format of the whole document mid-script.
[../adapters/document-shape.md](../adapters/document-shape.md) — the shape conversion output takes.
