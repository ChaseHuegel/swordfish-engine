# Improvement: Store the last applied look in a component

- Type: improvement
- Status: open
- Workflow: ../specs/issues.md

## Problem

`SimulationStep` keeps per-entity look totals in a side dictionary
(`_lastAppliedLook`) keyed by entity identity. The step prunes entries for
identities that no longer exist on an interval, because a stale entry is harmless
once the key is the identity rather than the store index.

This is ECS data living outside the store. The step needs a hand-written
lifecycle, and every reader has to trust that the prune keeps the dictionary
honest. The prune also costs a store lookup per tracked entity on the interval,
and allocates a list whenever an identity was removed.

A component beside `InputComponent` removes the dictionary, the prune, and the
allocation. The trade-off to design for: the step's query shape would require the
component, so a spawn path that forgets it would silently stop simulating the
entity. The fix must make that impossible or assert it.

## Acceptance criteria

- [ ] The last applied look totals live in the store, not a side dictionary.
- [ ] `PruneLookHistory` and its interval are deleted.
- [ ] Every spawn path that creates a simulated player adds the new component.
- [ ] The rejoin and recycled-index behavior pinned by
      `SimulationStepLookHistoryTests` still holds.
