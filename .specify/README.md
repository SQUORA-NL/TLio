# TLio SpecKit Setup

This directory contains the Spec-Driven Development (SDD) templates for the TLio project.

## Workflow

```
1. /speckit.specify  → specs/<NNN>-<feature>/spec.md
2. /speckit.plan     → specs/<NNN>-<feature>/plan.md
3. /speckit.tasks    → specs/<NNN>-<feature>/tasks.md
4. /speckit.implement → production code + tests
5. /speckit.review   → constitutional compliance check
```

## Templates

| Command | Template file | Purpose |
|---|---|---|
| `/speckit.specify` | `templates/speckit.specify.md` | Feature specification |
| `/speckit.plan` | `templates/speckit.plan.md` | Implementation plan |
| `/speckit.tasks` | `templates/speckit.tasks.md` | Task breakdown |
| `/speckit.implement` | `templates/speckit.implement.md` | Code generation |
| `/speckit.review` | `extensions/speckit.review.md` | Constitutional review |

## Specs index

| # | Feature | Status |
|---|---|---|
| 001 | TLio Core Architecture | In Progress — Phase 1 done (scaffold), Phase 2–7 pending |
| 002 | Migration from JLio | In Progress — specs + plan + tasks written; Phase 2A next |

## Key files

- `specs/constitution.md` — immutable architectural principles
- `specs/001-tlio-core-architecture/tasks.md` — Phase 1–7 implementation backlog
- `specs/002-migration-from-jlio/spec.md` — hard requirements for behavioral parity
- `specs/002-migration-from-jlio/plan.md` — JLio→TLio component mapping
- `specs/002-migration-from-jlio/tasks.md` — granular task list (73 test files to port)
- `specs/002-migration-from-jlio/porting-guide.md` — exact substitution table for test porting

## Hard requirement (from spec 002)

> All JLio unit tests, ported to TLio.UnitTests and run against the Newtonsoft adapter,
> must pass with **identical assertions**. No test may be weakened, removed, or have its
> assertion changed to accommodate an implementation difference.
