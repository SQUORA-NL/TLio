# /speckit.tasks — Derive a task list from an implementation plan

## Purpose
Convert `specs/<NNN>-<slug>/plan.md` into an actionable task list at
`specs/<NNN>-<slug>/tasks.md`.

## Instructions for AI

1. **Read** `plan.md` (and `data-model.md`, `contracts/` if present) before writing.
2. **One task = one atomic unit of work** — a developer should be able to complete it
   in a single coding session.
3. **Mark independent tasks with `[P]`** — these can be worked on in parallel.
4. **Order: tests before implementation** — for each feature, the test task precedes
   the implementation task.
5. **Use checkboxes** — `- [ ]` for pending, `- [x]` for done.
6. **Group tasks by phase** matching the plan's phases.

## Task format

```
- [ ] [P] <Imperative verb phrase> — <one-line acceptance criterion>
```

Examples:
```
- [ ] [P] Implement `Set<TNode>.Execute` — targets are selected via IItemsFetcher; value is applied via INodeAdapter.Replace
- [ ] Write `SetCommandTests` — all JLio SetTests pass against the JSON adapter
```

## Checklist (run before saving)
- [ ] Every plan deliverable maps to at least one task
- [ ] Every implementation task has a corresponding test task
- [ ] No task references a concrete format type (JToken, XElement, etc.) in Core/Commands tasks
- [ ] Future work that is out of scope is listed at the bottom under "Future Specs"
