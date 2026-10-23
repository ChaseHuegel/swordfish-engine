# Improvement: Remove the TODO markers from WaywardBeyond.Gameplay

- Type: improvement
- Status: open
- Workflow: ../specs/issues.md

## Problem

`WaywardBeyond.Gameplay/CharacterDefaults.cs` carries two `TODO` comments
introduced with the module. The development rules forbid merging a new `TODO` or
`FIXME`: the work is done or it is captured as an issue.

Both comments describe work already captured by issue 0054 (tunables belong in
config) and issue 0056 (body definitions own offsets and heights), so the markers
duplicate tracked work. They were kept in place deliberately during the review
pass.

## Acceptance criteria

- [ ] `grep -rn TODO WaywardBeyond.Gameplay` returns nothing.
- [ ] Both subjects are covered by 0054 and 0056.
- [ ] No behavior change.
