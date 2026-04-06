# Implementation Plan: AI Reference Documentation

**Branch**: `007-create-ai-ref-docs` | **Date**: 2026-04-06 | **Spec**: [spec.md](spec.md)

## Summary

Create 26 token-efficient markdown files under `docs/ai-ref/` that serve as the
authoritative reference for AI agents generating TLio scripts. Files cover all 14
commands (10 core + 4 ETL), 6 functions, 5 adapter variants, and 1 overview. No
production code changes; this is pure documentation output mandated by constitution
Article XI.

## Technical Context

**Language/Version**: Markdown (no code changes)
**Primary Dependencies**: N/A — reads existing source files only
**Storage**: File system — `docs/ai-ref/` directory tree
**Testing**: PowerShell compliance check (Article XI) + manual spot-check against fixtures
**Target Platform**: Repo documentation, consumed by AI agents
**Project Type**: Documentation
**Performance Goals**: N/A
**Constraints**: ≤150 lines per file; token-efficient prose; complete JSON examples
**Scale/Scope**: 26 files across 4 subdirectories

## Constitution Check

| Gate | Article | Question | Answer |
|---|---|---|---|
| Format Neutrality | I | Do any Core/Commands/Functions changes risk importing a format-specific type? | No — documentation only, zero code changes |
| Dependency Inversion | II | Is every new adapter/fetcher dependency injected? | N/A |
| Generic-First | III | Does every new public API carry `<TNode>`? | N/A |
| Process/Execution separation | IV | Do all node reads/mutations go through context? | N/A |
| Swappable Selection | V | Are paths supplied by callers? | N/A |
| Test-First + Fixture Triplets | VI | Will tests use fixture triplets? | N/A — no code; compliance check is the gate |
| Simplicity Gate | VII | Could this be simpler? | 26 files is the minimum; one per component |
| Backward Migration Path | VIII | Is any JLio behaviour changed/dropped? | No |
| No Leaking Internals | IX | Do Core APIs expose only TNode-parameterised types? | N/A |
| Logging as Observability | X | Does every Execute() path log correctly? | N/A |
| AI Component Reference | XI | Does every command/function/adapter have ai-ref.md? | **Yes — this plan creates them all** |

## Project Structure

### Documentation (this feature)

```text
specs/007-create-ai-ref-docs/
├── spec.md
├── plan.md              ← this file
├── research.md
├── contracts/
│   └── ai-ref-template.md
├── quickstart.md
└── checklists/
    └── requirements.md
```

### Source Code (repository root)

```text
docs/
└── ai-ref/
    ├── overview.md                       ← adapter selection + JSONPath table (1)
    ├── commands/
    │   ├── Set.md                        ← core commands (10)
    │   ├── Add.md
    │   ├── Put.md
    │   ├── Remove.md
    │   ├── Copy.md
    │   ├── Move.md
    │   ├── IfElse.md
    │   ├── Compare.md
    │   ├── Merge.md
    │   ├── DecisionTable.md
    │   ├── Flatten.md                    ← ETL commands (4)
    │   ├── Restore.md
    │   ├── Resolve.md
    │   └── ToCsv.md
    ├── functions/
    │   ├── Fetch.md                      ← built-in functions (6)
    │   ├── Indirect.md
    │   ├── Promote.md
    │   ├── Partial.md
    │   ├── ScriptPath.md
    │   └── Datetime.md
    └── adapters/
        ├── json-newtonsoft.md            ← adapter variants (5)
        ├── json-systemtext.md
        ├── xml-slashpath.md
        ├── xml-xpath.md
        └── yaml.md
```

**Structure Decision**: Single `docs/ai-ref/` tree with three subdirectories (commands,
functions, adapters) and one root file (overview.md). Matches the Canonical Sources table
in the constitution Governance section.

## Key Design Decisions (from research.md)

### Command JSON name conventions

All property names in command JSON use camelCase (matching `CommandConverter.ToPascalCase`
mapping). The `"command"` discriminator uses the registered string name exactly:
- Core: `set`, `add`, `put`, `remove`, `copy`, `move`, `ifElse`, `compare`, `merge`,
  `decisionTable`
- ETL: `flatten`, `restore`, `resolve`, `tocsv`

### `path` vs `property` in PropertyChangeCommand subclasses

`Set`, `Add`, `Put` support two forms:
1. `path` only — selects the node directly and sets it.
2. `path` + `property` — `path` selects a parent; `property` names the child key.

Both forms are documented in their respective files.

### Function call syntax

Functions are string values starting with `=`: `"value": "=fetch($.source)"`.
Arguments are comma-separated; paths use the same notation as `path` options.

### ETL registration

ETL commands require `options.CommandsProvider.RegisterETL<TNode>()` in addition to
`ParseOptions<TNode>.CreateDefault()`. This is documented in both the overview and each
ETL command's ai-ref.md.

### File naming

- Commands: PascalCase C# class name → `docs/ai-ref/commands/<ClassName>.md`
  (e.g., `DecisionTable.md`, not `decisionTable.md`)
- Functions: PascalCase C# class name → `docs/ai-ref/functions/<ClassName>.md`
- Adapters: kebab-case format+variant → `docs/ai-ref/adapters/<format-variant>.md`

## Complexity Tracking

No constitutional violations. This feature exists solely to implement Article XI.
