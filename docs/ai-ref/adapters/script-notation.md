# Script Notation — the same script in JSON, XML and YAML

A TLio script is a list of command objects. Each format spells that list in its own syntax, but
the command set, the property names and the semantics are identical: a script that works
against JSON has an XML and a YAML spelling that does the same thing.

Only the **paths** change, because each format has its own path language.

---

## The same script, three ways

<table>
<tr><th>JSON</th></tr>
<tr><td>

```json
[
  { "command": "set", "path": "$.name", "value": "Alice" },
  { "command": "add", "path": "$.address.city", "value": "Amsterdam" },
  { "command": "copy", "fromPath": "$.a", "toPath": "$.b", "destinationAsArray": true },
  { "command": "put", "path": "$.point", "value": { "x": 1, "y": 2 } },
  { "command": "ifElse",
    "condition": "=equals($.n, 5)",
    "ifScript":   [ { "command": "put", "path": "$.r", "value": "yes" } ],
    "elseScript": [ { "command": "put", "path": "$.r", "value": "no"  } ] }
]
```

</td></tr>
<tr><th>XML</th></tr>
<tr><td>

```xml
<script>
  <set path="/order/name">Alice</set>
  <add path="/order/address/city">Amsterdam</add>
  <copy fromPath="/order/a" toPath="/order/b" destinationAsArray="true"/>
  <put path="/order/point"><value><x>1</x><y>2</y></value></put>
  <ifElse condition="=equals(/order/n, 5)">
    <ifScript>  <put path="/order/r">yes</put></ifScript>
    <elseScript><put path="/order/r">no</put> </elseScript>
  </ifElse>
</script>
```

</td></tr>
<tr><th>YAML</th></tr>
<tr><td>

```yaml
- command: set
  path: $.name
  value: Alice
- command: add
  path: $.address.city
  value: Amsterdam
- command: copy
  fromPath: $.a
  toPath: $.b
  destinationAsArray: true
- command: put
  path: $.point
  value:
    x: 1
    y: 2
- command: ifElse
  condition: "=equals($.n, 5)"
  ifScript:
    - command: put
      path: $.r
      value: yes-branch
  elseScript:
    - command: put
      path: $.r
      value: no-branch
```

</td></tr>
</table>

YAML uses the same `$.a.b` paths as JSON. XML uses XPath — see
[document-shape.md](document-shape.md) for how an array subscript translates.

---

## The XML notation in detail

| What | How | Example |
|---|---|---|
| Command | child element of `<script>`, named after the command | `<set …>` |
| Scalar property | attribute | `path="/order/name"` |
| Boolean | attribute, `true` / `false` | `destinationAsArray="true"` |
| Enum | attribute, case-insensitive | `arrayMergeMode="replace"` |
| Value expression | attribute | `condition="=equals(/order/n, 5)"` |
| Scalar value | text content | `<set path="…">Alice</set>` |
| Structured value | `<value>` child element | `<value><x>1</x></value>` |
| Null value | empty `<value/>` | `<set path="…"><value/></set>` |
| Nested script | child element named after the property | `<ifScript>…</ifScript>` |
| Settings object | child element, same field names as JSON | see below |

A settings object is written with the same field names the JSON notation uses, following the
[document shape](document-shape.md) for nesting and lists:

```xml
<merge path="/root/source" targetPath="/root/target">
  <settings>
    <arraySettings>
      <item>
        <arrayPath>/root/target/items</arrayPath>
        <keyPaths><item>id</item></keyPaths>
      </item>
    </arraySettings>
  </settings>
</merge>
```

Child elements that do not name a command property are read as the value, so
`<set path="…"><a>1</a></set>` means the same as `<set path="…"><value><a>1</a></value></set>`.

---

## Paths inside function expressions

A function argument is treated as a path when it is written in the format's own path language:

| Format | A path argument looks like |
|---|---|
| JSON, YAML | `$.order.email`, `@.line` |
| XML | `/order/email`, `//email`, `./email` |

XML deliberately does **not** auto-detect a bare relative step: `email` is legal XPath but
indistinguishable from the text `email`, so it stays a value. Write it anchored.

```xml
<put path="/order/copy">=fetch(/order/email)</put>
```

---

## Limits worth knowing

- **XML has no bare path value.** In JSON, `"value": "$.a"` is a path. In XML, `/order/a` as
  text is text — use `=fetch(/order/a)`.
- **The `decisionTable` config** is only parseable from the JSON notation today.
- Escape sequences (`$$`, `@@`, `==`) are the JSON notation's; the other two have their own
  quoting.

## See Also

- [document-shape.md](document-shape.md) — how a document looks in each format
- [../notation-reference.md](../notation-reference.md) — value and function notation
