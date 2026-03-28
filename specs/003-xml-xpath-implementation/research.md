# Research: Native XPath Adapter — Path Semantics & Test Separation

**Feature**: 003-xml-xpath-implementation
**Date**: 2026-03-28

---

## Decision 1 — Two fetchers, not one

**Decision**: Keep `SlashPathItemsFetcher` (renamed from `XPathItemsFetcher`) and add
`NativeXPathItemsFetcher` as a second independent implementation.

**Why not replace**: Existing XML fixture files, `XmlScriptParser`, and any user scripts
already written use slash-path notation (`/address/city`). Replacing in place would be a
silent breaking change. Renaming makes the original behaviour self-documenting.

**Alternatives considered**:
- Auto-detect format at runtime — rejected: fragile, confusing, violates explicit-over-implicit
- Feature flag — rejected: adds runtime branching inside the fetcher; Article V requires the
  fetcher itself to be swappable, not internally conditional

---

## Decision 2 — NativeXPathItemsFetcher path semantics

### Root path indicator: `.`

XPath `.` means "current node" (self). When the root data element is passed as context,
`.` selects it. This maps cleanly to "give me the root" without overloading `/`.

### Absolute paths via XDocument wrapper

`XElement.XPathSelectElements("/root/name")` throws or returns nothing because there is no
XDocument to anchor absolute `/`. Solution: wrap the root XElement in a temporary XDocument
for the duration of the query:

```csharp
private static IEnumerable<XElement> Evaluate(string xPath, XElement root)
{
    if (xPath.StartsWith("/"))
    {
        var doc = new XDocument(new XElement("_doc_root_", root));
        // adjust path: /root/... becomes /_doc_root_/root/...
        // simpler: use descendant-or-self from XDocument directly
        return doc.XPathSelectElements(xPath.TrimStart('/'));
    }
    return root.XPathSelectElements(xPath);
}
```

Simpler alternative: disallow absolute paths and document that native XPath paths must be
relative. This avoids wrapping complexity and aligns with common XPath usage against element
contexts. **Chosen: relative-only in v1, absolute paths documented as unsupported.**

### `GetPath(node)` output format

Produces `address/city` (no leading separator). This is the inverse of what callers write in
scripts; `SelectNodes(GetPath(node), root)` round-trips correctly.

### `EnsurePath` limitations

`EnsurePath` can only create simple `a/b/c` chains. Paths containing XPath predicates
(`item[@id='1']`) or axes (`descendant::`) are read-only — `EnsurePath` is a no-op for
them (Copy/Move commands log a warning when destination cannot be created).

### `SplitParentAndLeaf` — bracket-aware split

Split at the last `/` that is not inside square brackets. This handles `items/item[1]`
correctly: parent = `items`, leaf = `item[1]`.

---

## Decision 3 — Test project structure

### `TLio.Xml.Tests`

References: `TLio.Xml`, `TLio.Client`, NUnit.

Sub-namespaces:
- `TLio.Xml.Tests.SlashPath` — fixture tests for the existing slash-path fetcher
- `TLio.Xml.Tests.NativeXPath` — fixture tests for the new native XPath fetcher
- `TLio.Xml.Tests.Adapter` — unit tests for `XmlNodeAdapter` (shared between both fetchers)

Fixture file format: same single-file `fixture.xml` format, but NativeXPath variants live
under `Fixtures/XPathSet/`, `Fixtures/XPathAdd/` etc. with paths written as `address/city`
instead of `/address/city`.

### `TLio.Yaml.Tests`

References: `TLio.Yaml`, `TLio.Client`, NUnit.

Mirrors the YAML-related content currently in `TLio.UnitTests/Fixtures/Yaml*` and
`TLio.UnitTests/AdapterTests/Yaml*`.

### `TLio.UnitTests` after migration

Remove `ProjectReference` to `TLio.Xml` and `TLio.Yaml`. Remove XML/YAML fixture folders
and loader/test files. Core, command, function, and JSON adapter tests remain unchanged.

---

## Decision 4 — `XmlExecutionContext` factory naming

| Method | Fetcher | Path style |
|---|---|---|
| `CreateWithSlashPaths()` | `SlashPathItemsFetcher` | `/address/city` |
| `CreateWithNativeXPath()` | `NativeXPathItemsFetcher` | `address/city`, `//name`, `item[@id='1']` |

`CreateDefault()` kept as an alias for `CreateWithSlashPaths()` to avoid breaking existing
callers until a deprecation notice is added.
