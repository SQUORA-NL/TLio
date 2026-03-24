# /speckit.review — Review code for constitutional compliance

## Purpose
Check recently written or modified code against the TLio constitution and flag
any violations before they are committed.

## Instructions for AI

1. Read `specs/constitution.md`.
2. For each file under review, check each article in turn.
3. Report violations as a numbered list with the article number, file path, line
   reference, and a concrete fix suggestion.
4. If no violations are found, state "No constitutional violations found."

## Automated check (run first)

```bash
# Should return zero results for any file in Core / Commands / Functions
grep -rn "JToken\|JObject\|JArray\|JValue\|XElement\|YamlNode\|Newtonsoft\|System\.Xml\|YamlDotNet" \
     TLio.Core/ TLio.Commands/ TLio.Functions/
```

## Checklist

- [ ] **Art. IV (primary)** — No `TNode` variable is the receiver of a method call anywhere
      in TLio.Core, TLio.Commands, or TLio.Functions. Pattern to look for:
      `someNodeVariable.AnyMethod(` — this is always a violation if `someNodeVariable : TNode`.
- [ ] Art. I — No format-specific `using` statements in TLio.Core or TLio.Commands
- [ ] Art. II — No `new ConcreteAdapter()` outside of adapter projects and factories
- [ ] Art. III — All new public APIs are generic over TNode; no `object` casts
- [ ] Art. V — Path selection uses only `context.ItemsFetcher`; no hard-coded path syntax
- [ ] Art. VI — Tests exist for the feature before implementation was written
- [ ] Art. IX — No format-specific types appear as field/property/parameter types in Core or Commands
- [ ] Art. X — Every `Execute` method logs at least one entry on the success path
