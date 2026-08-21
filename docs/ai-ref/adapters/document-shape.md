# Document Shape — how one document looks in JSON, XML and YAML

Every command in TLio is written against the JSON data model: a node is an **object**, an
**array**, a **scalar**, or **null**. That model is what `add`, `set`, `merge` and the rest
reason about, and it is the only thing they see — the adapter answers "which of those is this
node?" on the format's behalf.

So a command behaves the same across formats exactly as far as the three adapters agree on that
answer. This page is that agreement.

---

## The mapping

| JSON | XML | YAML |
|---|---|---|
| `{"k": "v"}` | `<k>v</k>` | `k: v` |
| `{"k": 42}` | `<k>42</k>` | `k: 42` |
| `{"k": true}` | `<k>true</k>` | `k: true` |
| `{"k": {…}}` | `<k>` + one child element per property | `k:` + indented block |
| `{"k": [1,2]}` | `<k><item>1</item><item>2</item></k>` | `k:` + `- 1` / `- 2` |
| `{"k": null}` | `<k/>` | `k: null` |

The root object is the document element (`<root>` unless the document names it otherwise).

---

## Arrays in XML

An array is **one element whose children are the items** — the wrapper convention:

```xml
<order>
  <lines>
    <item><sku>A</sku></item>
    <item><sku>B</sku></item>
  </lines>
</order>
```

`$.lines` is `/order/lines`, and `$.lines[0].sku` is `/order/lines/item[1]/sku` — XPath
positions are 1-based where JSONPath indices are 0-based.

This is the only shape that gives an array a node of its own, which is what
`AppendToArray`, `GetArrayLength` and the array-aware commands need to address.

### Which elements count as an array

An element is an array when **all of its children share one element name** and either

- there is **more than one** of them, or
- that name is `item`, the canonical item name.

Everything else with children is an object.

That second clause is what separates the two documents XML cannot otherwise tell apart:

| XML | Reads as | Why |
|---|---|---|
| `<address><city>A</city></address>` | object | one child, not named `item` |
| `<items><item>A</item></items>` | array of 1 | one child named `item` |
| `<orders><order>A</order><order>B</order></orders>` | array of 2 | repeated name |
| `<address><city>A</city><zip>B</zip></address>` | object | two different names |

### Item naming when TLio writes

Adding to an array keeps the item name already in use, so an array of `<order>` grows with
`<order>`. An array with nothing in it gets `item`.

**The consequence to know about:** an array of one, in a document TLio did not write, is
indistinguishable from an object with one property unless the item is named `item`. If you
control the source shape and want single-element arrays to survive, name the items `item`.

### Addressing a position

A trailing integer subscript names a position in the array, in every format:

| JSON / YAML | XML |
|---|---|
| `$.items[0]` | `/order/items/item[1]` |
| `$.a.items[2].n` | `/order/a/items/item[3]/n` |

XPath writes the subscript on the item step and counts from one; JSONPath and the YAML
dot-notation write it on the array and count from zero. The fetchers normalise both, so a
command sees the same array and the same zero-based position whatever the format.

Writing through one behaves the same everywhere:

| | at an occupied position | at the next free position | further out |
|---|---|---|---|
| `set` / `put` | writes the element | no-op, warns | no-op, warns |
| `add` | skipped — it already exists | appends | no-op, warns |

`add` creates the array when it is not there, the same way it creates the objects along
`$.address.city`, so an array can be filled in order one command at a time:

```json
[ { "command": "add", "path": "$.tags[0]", "value": "frontend" },
  { "command": "add", "path": "$.tags[1]", "value": "safari"   } ]
```

It refuses any other missing position rather than appending — the element would land at an index
the path did not name.

Only an integer counts as a position. `items[*]`, `item[@id='1']` and `$['a.b']` name something
else, and each format's own selector handles them.

---

## The empty element

`<k/>` is the one place XML genuinely cannot carry the JSON model. It is equally the
serialisation of

```
null      ""      {}      []
```

There is no attribute-free way to tell them apart, so rather than pick one meaning and break
the other three, each question gets the answer that is true for it:

| Question | Answer for `<k/>` | Why |
|---|---|---|
| `IsObject` | yes | it is a container nothing has been written into yet |
| `IsNull` | yes | it carries no value |
| `IsPrimitive` | yes | it has no child elements |
| `IsArray` | no | an array of nothing is not addressable as one |
| `GetNodeKind` | `Null` | the type predicates need one answer, and null is the useful one |

`IsObject` being true is what makes path construction work. `add /order/address/city` builds
`<address/>` first and then writes the city into it — if that element read as a primitive, the
value would be dropped and the document left with an empty `<address/>`.

`GetNodeKind` returning `Null` is what keeps `=isNull($.k)` honest.

**Whitespace-only content counts as empty — for classification.** Parsing normally strips
insignificant whitespace, but a tree loaded with `LoadOptions.PreserveWhitespace` or built by
hand still carries it, and `<k>\n</k>` classifies exactly like `<k/>`: `IsObject` ignores
whitespace-only text, so the element stays a writable container. Only classification ignores
it — as a *value* (`TryGetString`) the text is kept, so a deliberate `" "` string survives.

**What this costs.** After `remove` empties an object, XML holds `<a/>` where JSON holds `{}`,
and re-reading that document gives `null`. Round-tripping an empty container through XML does
not preserve which empty thing it was. Nothing else depends on it.

YAML has none of this: `null`, `""`, `{}` and `[]` are four distinct scalars there, and the
YAML adapter reports them as such.

---

## Null is an unfilled container — in every format

The empty element answering `IsObject` with yes is not an XML quirk: the rule is format-wide.
For the commands that build structure — `add` and `put` — a **null-valued node is a container
nothing has been written into yet**, whether it is `<k/>`, `{"k": null}` or `k: null`:

| Onto `{"k": null}` | Result |
|---|---|
| `add $.k.demo = 3` | `{"k": {"demo": 3}}` — upgraded to an object |
| `add $.k[0] = 1` | `{"k": [1]}` — position `[0]` upgrades it to an array |
| `add $.k.a.b = 3` | `{"k": {"a": {"b": 3}}}` — a deep path builds through the null |
| `set $.k.demo = 3` | no-op, warns — `set` never builds |

Without this, one document written three ways behaved differently: XML created the property
while JSON and YAML warned "cannot add property to a primitive node".

The null document **root** is the one exception, in every format — nothing holds it, so there
is nothing to swap an object in for.

---

## Attributes

XML attributes are outside the JSON data model and the adapter ignores them: they are not
properties, they are not read, and no command writes one. An attribute already on an element
is left alone by everything except a command that replaces that element wholesale.

`FormatConverter` (see `FormatConverter/`) does carry attributes, for conversion work. That is a
separate concern from script execution, and it produces an **asymmetry worth knowing**:

| | attributes are | so a command can |
|---|---|---|
| in XML | outside the data model | not read or write them |
| after `convert` to JSON or YAML | ordinary `@name` properties | read and write them like any other |

So `xml → json`, edit `@id`, `json → xml` works, while editing `@id` *while the document is
XML* does not. Converting is the way to touch an attribute from a script.

An element that carries both attributes and text cannot be a bare scalar elsewhere — it becomes
`{"@currency": "EUR", "#text": "9.99"}`, with the value's key named by the `textProperty`
setting. Namespace declarations travel the same way, under `namespacePrefix`.

**Conversion output is this page.** `convert` writes the canonical shape above — arrays wrapped,
`item` as the item name, `<k/>` for null — precisely so that the commands *after* a mid-script
`convert` address the tree described here. `CanonicalShapeTests` in `FormatConverter.Tests`
walks converted output with `XmlNodeAdapter` and `YamlNodeAdapter` and fails when it drifts.
The legacy repeated-sibling shape is still reachable as `arrayHandling: "repeated"`, and is not
the default because in that shape the parent element is what reads as the array.

---

## Scalars are text

XML and YAML scalars have no declared type. `<n>42</n>` and `n: 42` are the characters `42`,
and the type predicates report the value's **apparent** type — `=isNumber()` is true for both.
JSON reports the document's own type, so `{"n": "42"}` is a string there and a number in the
other two. This is inherent, and documented under [NodeKind](../TLio_AI_Reference.md).

---

## Verifying it

`TLio.Parity.Tests` holds one fixture corpus, written once in JSON, and runs every case against
all three formats through each format's own script notation. `CanonicalShape` is this page in
code — it is what turns a fixture into the XML and YAML it should produce.

Alongside it, `Sweep/sweep.json` is a single script that touches **every registered command and
every registered function**, run from an empty document against all three. Two tests read the
command and function registries and fail when something is added without being swept, so the
coverage cannot lapse; a third asserts that the three recorded results differ only where a
format genuinely cannot agree.

A format that stops agreeing fails there.

## See Also

- [xml-slashpath.md](xml-slashpath.md), [xml-xpath.md](xml-xpath.md) — XML path syntax
- [yaml.md](yaml.md) — YAML path syntax
- [script-notation.md](script-notation.md) — the same script in all three notations
- [../../behaviour-decisions.md](../../behaviour-decisions.md) — known divergences still open
