# Data Model: Unified Script Notation

**Branch**: `010-unify-script-notation` | **Date**: 2026-04-20

This feature is primarily a documentation and minor parser validation feature. There is no new runtime data model. The entities below describe the **conceptual model** of the notation rules themselves — used to structure the notation reference document and tests.

---

## Notation Rule Taxonomy

### Value Context

Every property in a TLio command object is a **JSON value**. The `value`/`condition`/`defaultValue` fields go through `FunctionConverter.ParseValue`, which applies the following classification:

```
ScriptValue
├── NullValue            — JSON null
├── JsonLiteralValue     — JSON boolean, number, array, or object (not a string)
├── FunctionValue        — string starting with "="  →  parsed as function expression
├── PathValue            — string starting with "$" or "@"  →  passed to items fetcher
├── FixedValue           — everything else  →  treated as literal string
│   ├── QuotedFixed      — string wrapped in '...' or "..."
│   ├── EscapedFixed     — string starting with @@, $$, ==
│   └── PlainFixed       — all other strings (fallback)
```

### Argument Context (inside a FunctionValue)

Inside a function expression like `=concat(A, B, C)`, each argument is parsed by `SplitArgs` + `ParseValue` recursively. The same classification applies, with the additional rule:

- **`'...'` is the required delimiter for literal string arguments** because the outer JSON string already uses `"..."`.
- Path arguments (`$.field`, `@.field`) are never quoted.

### Relative Path Variants

| Form | Context | Valid in |
|------|---------|---------|
| `@.propertyName` | Current node, one level down | JSON, YAML |
| `@.<--` | Parent of current node | JSON, YAML |
| `@.<--.sibling` | Sibling via parent navigation | JSON, YAML |
| `@attrName` (no dot) | XPath attribute selector (inside `[...]`) | XML XPath only |
| `@@` | Escape: produces literal `@` | All contexts |

### Escape Sequences

| Escape | Applies in | Result |
|--------|-----------|--------|
| `@@` | Value level OR inside `'...'` | Literal `@` |
| `$$` | Value level OR inside `'...'` | Literal `$` |
| `==` | Value level OR inside `'...'` | Literal `=` |

---

## Notation Reference Document Structure

The new `docs/ai-ref/notation-reference.md` will follow this structure:

1. **JSON-First Rule** — scripts are valid JSON arrays; all expressions are JSON strings
2. **Value Types** — table of all ScriptValue subtypes with when each applies
3. **Function Expressions** — `=name(arg, ...)` syntax, when `=` is required
4. **Quoting Rules** — why `'...'` is used for literal args (outer `"..."` context)
5. **Path References** — `$.field`, `@.field` (with dot required), `@.<--`
6. **Escape Sequences** — `@@`, `$$`, `==` in both value and quoted-arg contexts
7. **`@` Roles Summary** — three roles with context column
8. **Bracket Notation** — `$['property.with.dot']` for special property names
9. **XPath Special Cases** — `@attr` inside predicates (XML-only)
