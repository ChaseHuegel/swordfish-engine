# Improvement: Clamp item stack capacity on insert

- Type: improvement
- Status: open
- Workflow: ../specs/issues.md

## Problem

`InventoryResolver.Apply` derives stacking room as `destination.MaxSize -
destination.Count`. A stack whose `MaxSize` is zero is therefore permanently
unstackable, because the available room is negative. Nothing stops such a stack
from entering an empty slot: the empty-destination branch copies the whole
`ItemData`, including `MaxSize`.

The value comes from content definitions and saved data. A definition that omits
`MaxSize` yields zero, and the resulting stack can never merge again.

## Acceptance criteria

- [ ] An inserted stack carries a positive `MaxSize`, or the insert is rejected.
- [ ] A stack with capacity zero can still be moved and removed.
- [ ] A test pins the behavior for a zero-capacity source stack.
