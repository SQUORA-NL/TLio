# /speckit.specify — Create a new TLio feature specification

## Purpose
Transform a plain-language feature description into a structured TLio specification
file under `specs/<NNN>-<feature-slug>/spec.md`.

## Instructions for AI

1. **Number the spec** by scanning `specs/` for the highest existing `NNN` prefix and incrementing.
2. **Create the branch name** as `feat/<NNN>-<feature-slug>` derived from the description.
3. **Read the constitution** at `specs/constitution.md` before writing.
4. **Populate `spec.md`** using the template below.
5. Mark any ambiguities with `[NEEDS CLARIFICATION: <specific question>]`.
6. Focus exclusively on WHAT and WHY — no implementation details, no tech stack decisions.

## Template

```markdown
# Spec <NNN> — <Feature Title>

**Branch:** `feat/<NNN>-<slug>`
**Status:** Draft

---

## Overview

<2–4 sentence description of the feature and its business rationale.>

---

## User Stories

### US-01 — <Short title>

As a <role>,
I want to <action>
so that <outcome>.

**Acceptance criteria:**
- <Criterion 1>
- <Criterion 2>
- <Criterion 3>

<!-- Add more US blocks as needed -->

---

## Non-Functional Requirements

- <Performance / security / compat constraint>

---

## Out of Scope

- <What this spec deliberately does NOT cover>
```

## Checklist (run before saving)
- [ ] Every acceptance criterion is testable (can be written as a unit test)
- [ ] No HOW — no class names, method signatures, or library choices in spec.md
- [ ] All ambiguities are marked with [NEEDS CLARIFICATION]
- [ ] Spec is consistent with specs/constitution.md
