# /speckit.plan — Create an implementation plan for a TLio spec

## Purpose
Translate the feature specification at `specs/<NNN>-<slug>/spec.md` into a technical
implementation plan at `specs/<NNN>-<slug>/plan.md`.

## Instructions for AI

1. **Read** `specs/constitution.md` and the target `spec.md` before starting.
2. **Run the Phase -1 gates** — document your answers in the plan.
3. **Map user stories → technical decisions**, always providing explicit rationale.
4. **List phases** with clear prerequisites and deliverables.
5. **List technology choices** in the table, with reasoning tied to the constitution.
6. Keep `plan.md` high-level. Move any code samples or detailed algorithms to
   `specs/<NNN>-<slug>/implementation-details/`.

## Constitutional Pre-checks

Answer these before writing the plan:

- **Format Neutrality (Art. I):** Which projects will be modified? Do any Core/Commands
  changes risk importing a format-specific type?
- **Dependency Inversion (Art. II):** Is every new dependency injected? Any `new` of
  a concrete adapter?
- **Generic-First (Art. III):** Does every new public API carry `TNode` as a generic param?
- **Simplicity Gate (Art. VII):** Could this be done with fewer projects/layers?

## Template

```markdown
# Plan <NNN> — <Feature Title>

**Spec:** [spec.md](./spec.md)
**Status:** Draft

---

## Phase -1: Constitutional Gates

### Simplicity Gate
<justify the number of projects / layers>

### Anti-Abstraction Gate
<confirm no unnecessary wrappers>

### Integration-First Gate
<confirm tests use real adapters, not mocks, for happy-path>

---

## Architecture Overview

<Diagram or prose describing structure and data flow>

---

## Technology Choices

| Concern | Choice | Rationale |
|---|---|---|

---

## Phase-by-Phase Breakdown

### Phase N — <Name>
**Deliverables:**
- <item>

---

## Complexity Tracking

<Document any justified exceptions to constitutional articles>
```
