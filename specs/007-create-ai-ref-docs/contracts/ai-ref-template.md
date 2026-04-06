# AI Reference File Contract

This document defines the exact structure every `ai-ref.md` file MUST follow
(per constitution Article XI). It is the binding contract for all 26 files in
`docs/ai-ref/`.

---

## Command file template (`docs/ai-ref/commands/<Name>.md`)

```markdown
# <commandName>

> <One-sentence purpose: what it does and when to use it over alternatives.>

## Syntax

```json
{ "command": "<name>", "<option1>": <value>, "<option2>": <value> }
```

## Options

| Option | Type | Required | Default | Description |
|--------|------|----------|---------|-------------|
| <option> | string\|boolean\|object | yes\|no | —\|<default> | <description> |

## Formats

Works with all adapters. Path syntax differs per adapter — see
[overview.md](../overview.md) for the adapter selection table.

## Example

```json
{ "command": "<name>", "<option>": <value> }
```
```

**Rules:**
- Heading level 1 = command name in its registered JSON form (e.g., `ifElse`, not `IfElse`).
- Purpose line MUST distinguish from similar commands (e.g., Set vs Put vs Add).
- `value` type must be shown as `TLioValue` when it accepts functions or literals.
- If a command has a two-argument path/property form, show both forms in separate code blocks.
- Max 150 lines total.

---

## Function file template (`docs/ai-ref/functions/<Name>.md`)

```markdown
# =<functionName>()

> <One-sentence purpose.>

## Syntax

```
=<functionName>(<arg1>, <arg2?>)
```

Used as a value in any command: `"value": "=<functionName>($.path)"`

## Arguments

| # | Type | Required | Description |
|---|------|----------|-------------|
| 1 | string (path) | yes | <description> |
| 2 | integer | no | <description> |

## Returns

<Type and description of the returned value.>

## Example

```json
{ "command": "set", "path": "$.target", "value": "=<functionName>($.source)" }
```
```

**Rules:**
- Heading uses the call prefix `=` to make it immediately recognisable as a function.
- Arguments table uses positional numbering, not names, since functions use ordered args.
- Returns section MUST describe the type (string, number, boolean, object, node).
- Max 150 lines total.

---

## Adapter file template (`docs/ai-ref/adapters/<name>.md`)

```markdown
# TLio.<Format> Adapter — <variant>

> <One-sentence description of format + path style.>

## Setup

```csharp
var options = ParseOptions<TNode>.CreateDefault();
var context = <ContextClass>.<FactoryMethod>(data, script, options);
```

## Path Syntax

| Pattern | Example | Matches |
|---------|---------|---------|
| Root    | `$` or `.` | document root |
| Child   | `$.name` | direct child `name` |
| ...     | ...     | ... |

## Notes

<Up to 3 bullet points of format-specific limitations or behaviours.>

## See Also

[overview.md](../overview.md) — adapter selection table and JSONPath comparison.
```

**Rules:**
- Setup block uses concrete class names from the adapter project.
- Path Syntax table covers at minimum: root, direct child, nested, array index,
  wildcard, recursive (if supported).
- Notes section MUST include any known limitations vs other adapters.
- Max 150 lines total.

---

## Overview file contract (`docs/ai-ref/overview.md`)

Must contain in this order:
1. One-paragraph TLio introduction (≤5 sentences).
2. **Adapter Selection table** (format / adapter project / factory method / path style).
3. **JSONPath: Newtonsoft vs System.Text.Json** comparison table (8+ feature rows).
4. **Script format** section with a minimal complete JSON example.
5. **ETL extension pack** section documenting how to register the 4 ETL commands.

Max 150 lines total.
