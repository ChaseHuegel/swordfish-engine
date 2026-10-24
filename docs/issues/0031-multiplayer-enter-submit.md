# Improvement: Multiplayer page — Enter submits connect

- Type: improvement
- Status: done
- Workflow: ../specs/issues.md

## Problem

Playtest: pressing Enter on the multiplayer page should submit the connect
action. Today connect is button-only; the address and port input fields
don't submit on Enter (keyboard navigation isn't wired to the connection
flow).

## Acceptance criteria

- [x] Pressing Enter while the address or port field has focus triggers
      the same connect path as the connect button (including validation of
      an empty or invalid address).
- [x] The key handling doesn't conflict with existing page navigation
      (e.g., Esc/back behavior unchanged).
- [x] Manual verification: focus address, type, Enter -> connect starts;
      same from the port field.
- [x] `networking-join.md` notes the keyboard submit affordance if the
      multiplayer page flow is documented there (docs pass).