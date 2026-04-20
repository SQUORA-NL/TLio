# Contract: Notation Reference Document API

**Feature**: `010-unify-script-notation`  
**Type**: Documentation contract — defines the required structure and content of `docs/ai-ref/notation-reference.md` and the conventions all function/command `ai-ref.md` files must follow.

---

## notation-reference.md Required Structure

```
# TLio Notation Reference

## 1. JSON-First Rule
## 2. Value Types
## 3. Function Expressions
## 4. Quoting Rules
## 5. Path References
## 6. Escape Sequences
## 7. The Three Roles of @
## 8. Bracket Notation
## 9. XPath Special Cases (XML only)
```

Every section must:
- Include at least one concrete, valid JSON snippet example
- Stay within 20 lines (notation reference is a reference, not a tutorial)
- Reference `docs/ai-ref/overview.md` for adapter-specific details rather than duplicating the adapter table

---

## Convention: Function/Command ai-ref.md Syntax Notation

All `## Syntax` sections in function and command `ai-ref.md` files MUST follow this convention:

```
=functionName(<arg1>, <arg2>)
=functionName(<arg1>, <arg2>, <argN>, ...)   — variadic
```

Where `<argN>` is a placeholder name. The Arguments table clarifies the type.

**Forbidden forms** (must be replaced):
- `=concat(a, b)` — looks like unquoted literals; replace with `=concat(<value1>, <value2>)`
- `=concat(a, b, c, ...)` — same issue

---

## Convention: Example Format in ai-ref.md Files

All examples MUST use the following pattern:

```json
{ "command": "<name>", "path": "$.targetField", "value": "=funcName($.pathArg, 'literalArg')" }
```

Rules:
- Path arguments (e.g., `$.first`, `@.child`) are NEVER quoted inside the expression
- Literal string arguments (e.g., `' '`, `'Unknown'`, `'prefix-'`) are ALWAYS single-quoted inside the expression
- The outer JSON string uses double quotes (standard JSON)
- No example may use outer single quotes around a function expression

---

## Convention: Notation Note

All function and command `ai-ref.md` files with a `value` or expression argument MUST include this standard note in the `## Syntax` section:

```
> See [Notation Reference](../notation-reference.md) for quoting rules and escape sequences.
```

---

## Parser Validation Contract (FunctionConverter.cs)

When a script value starts with `@` and is NOT:
- Followed by `.` (relative path `@.field`)
- `@@` (escape sequence)
- Used inside a JSONPath filter expression (context already handles this)

Then `FunctionConverter.ParseValue` MUST log a warning via `IExecutionLogger`:

```
"Path '@<name>' is missing the required dot — did you mean '@.<name>'? Relative paths in JSON/YAML require the '@.' prefix."
```

The value still passes through as a `PathValue` (no breaking change), but the warning guides the developer.

---

## Test Fixture Contract

Each new notation test scenario MUST be a file-based fixture triplet with this naming:

```
<TestProject>/Fixtures/Notation/<scenario-name>/
  input.json
  script.json
  result.json
```

Required scenarios (see research.md §3):
- `@dotpath-valid` — `@.field` relative path works correctly
- `escape-at-value` — `@@` at value level produces literal `@`
- `escape-in-quoted-arg` — `'user@@example.com'` inside a function arg
- `outer-quoted-function` — `"'=concat(...)'"` treated as a literal string, not a function
