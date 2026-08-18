# TLio Quick-Decision Guide

> Use this guide to select the right command or function before writing a script.
> For full documentation including examples and common mistakes, call tlio_describe('CommandOrFunctionName').

---

## Command Decision Tree

### Writing a property value

| Intent | Command | Trace when wrong |
|--------|---------|-----------------|
| Create field — skip if it already exists | `add` | noop "already exists" → field was already set |
| Update field — warn if it does not exist | `set` | noop "property not found" → path is wrong or field missing |
| Create-or-update, don't care which | `put` | always writes — safe default when uncertain |

**Quick rule:** When in doubt, use `put`. Use `add` when idempotent create is required. Use `set` when you want explicit failure on a missing path.

### Moving or deleting nodes

| Intent | Command | Note |
|--------|---------|------|
| Copy node, keep source | `copy` | Source remains in document |
| Relocate node, remove source | `move` | Source is deleted after copy |
| Delete node entirely | `remove` | noop (not failure) if path not found |

### Conditional logic

| Branches | Rule evolution | Command |
|----------|----------------|---------|
| Two outcomes, one condition | Simple | `ifElse` |
| Three or more outcomes | May grow | `decisionTable` |
| Produce equal/greater/less/different label | Classification | `compare` |

### Combining data

| Intent | Command |
|--------|---------|
| Deep-merge two objects | `merge` |
| Merge two collections by element key | `merge` with `settings.arraySettings[].keyPaths` |
| Append to a collection without duplicates | `merge` with `settings.arraySettings[].uniqueItemsWithoutKeys` |
| Apply defaults without overwriting | `merge` with `settings.strategy = "onlyStructure"` |
| Lookup join (enrich from reference collection) | `resolve` |

### ETL / serialization

| Intent | Command | Constraint |
|--------|---------|------------|
| Flatten nested object to key-value | `flatten` | Use IncludeMetadata=true if you plan to restore |
| Reconstruct nested from flat | `restore` | Requires metadata from flatten |
| Export array to CSV string | `tocsv` | Terminal step only — do not use mid-pipeline |

---

## Function Decision Tree

### String checks (all return boolean)

| Goal | Function | Null-safe? |
|------|----------|-----------|
| Substring anywhere in string | `contains` | No — guard with isEmpty first |
| String starts with prefix | `startsWith` | No |
| String ends with suffix | `endsWith` | No |
| Position of substring (-1 if absent) | `indexOf` | No |
| Null/empty/whitespace check | `isEmpty` | **Yes** — the only null-safe predicate |

### String assembly

| Goal | Function |
|------|----------|
| Join 2–4 fixed fields with any separators | `concat` |
| Join dynamic array with single separator | `join` |
| Fill {0}, {1}… placeholders in a template | `format` |

### String decomposition

| Goal | Function |
|------|----------|
| Split on delimiter → array | `split` |
| Extract by start position + length | `substring` |
| Find position of substring | `indexOf` |

### Case and whitespace

| Goal | Function |
|------|----------|
| Remove both-end whitespace | `trim` |
| Remove leading whitespace only | `trimStart` |
| Remove trailing whitespace only | `trimEnd` |
| Convert to lowercase | `toLower` |
| Convert to uppercase | `toUpper` |

### Numeric aggregates

| Goal | Function | Fails if empty/missing? |
|------|----------|------------------------|
| Total | `sum` | Yes |
| Mean (outlier-sensitive) | `avg` | Yes |
| Middle value (outlier-robust) | `median` | Yes |
| Count of items (any type) | `count` | No |
| Smallest value | `min` | Yes |
| Largest value | `max` | Yes |
| Filtered total | `sumif` | Yes — arrays must be parallel |
| Filtered count | `countif` | Yes |

### Rounding

| Goal | Function | 7.5 → | -7.5 → |
|------|----------|--------|---------|
| Nearest integer | `round` | 8 | -8 |
| Always up | `ceiling` | 8 | -7 |
| Always down | `floor` | 7 | -8 |

### Date and time

| Goal | Function | Returns |
|------|----------|---------|
| Is date within a range? | `isDateBetween` | boolean (both bounds inclusive) |
| Which date is earlier/later? | `dateCompare` | **long** -1/0/1 — NOT a string |
| Earliest date in array | `minDate` | date string |
| Latest date in array | `maxDate` | date string |
| Average date of array | `avgDate` | date string |
| Current UTC timestamp | `datetime` | date string (always UTC) |

### Advanced / path functions

| Goal | Function |
|------|----------|
| Copy one value inline (first match) | `fetch` |
| Dynamic path stored in document data | `indirect` |
| Select Nth result from wildcard path | `partial` |
| Get absolute path of current node | `scriptpath` |
| Wrap node in a parent object | `promote` |
| Generate a unique UUID | `newguid` |
| JSON string → structured node | `parse` |
| Any node → JSON string | `toString` |

---

## Critical Rules

1. **Function path resolution uses document ROOT.**
   Inside any function, `@.field` resolves against `$` (the root), not the current array element.
   To apply a function per element: use absolute indexed paths (`$.items[0].price`, `$.items[1].price`).
   Wildcard paths (`$.items[*].price`) inside a function produce a flat list — correct for aggregates (`sum`, `count`), wrong for per-element transforms.

2. **Function value syntax:** `"value": "=functionName(arg1, arg2)"` — a JSON string starting with `=`.

3. **add vs set vs put at a glance:**
   - `add` on existing → **noop** ("already exists") — not an error
   - `set` on missing → **noop** ("property not found") — fix the path
   - `put` → always writes — use when uncertain

4. **Trace outcomes:**
   - `"success"` — data was changed
   - `"noop"` — command ran but nothing changed (path mismatch or skip condition)
   - `"failure"` — command could not run (validation error, missing required property, function path not found)

5. **decisionTable result values must be plain primitives.**
   `"results": {"tier": "gold"}` ✓ — `"results": {"tier": {"value": "gold"}}` ✗ (writes entire object)

6. **ifElse condition must be a JSON primitive.**
   `"condition": true` ✓ — `"condition": {"value": true}` ✗

7. **dateCompare returns a long integer (-1, 0, or 1), never a string.**

8. **Scripts are JSON arrays of command objects.**
   Each command has a `"command"` key. All property names are camelCase.

---

Call `tlio_describe('CommandName')` for full documentation, examples, and common mistakes for any specific command or function.
