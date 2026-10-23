# Improvement: Make the NetworkComponent invariants real

- Type: improvement
- Status: open
- Workflow: ../specs/issues.md

## Problem

Two defects remain on `WaywardBeyond.Gameplay/Networking/NetworkComponent.cs`,
now that the consumed sequence watermarks live there.

First, the type has an invalid default state. All three staging rings are
nullable, and every consumer guards them (`ServerInteractionSystem`,
`ServerInventorySystem`, `NetworkReplicationSystem`). `Create()` is the only valid
construction, but nothing enforces it: a default or store-materialized instance
silently disables staging. A getter with lazy initialization cannot fix this,
because the struct is read through `in` and a mutating getter would be lost to a
copy.

Second, `Session` is write-only state. `NetworkComponent.Session` is stamped by
`SessionManager.Register` and never read, and `Session.ID` has no reader.
`SessionManager` keeps `_clientToSession` only as a presence set for `Count` and
`EndSession`, so a `HashSet<Uuid>` would do the same job.

## Acceptance criteria

- [ ] A `NetworkComponent` either guarantees its rings are non-null, or documents
      the null default as the deliberate "not staged" state and removes the
      per-consumer guards.
- [ ] `Session` is consumed by a real lookup, or deleted along with
      `NetworkComponent.Session` and `SessionManager._clientToSession`.
- [ ] No consumer still null-checks a staging ring without a documented reason.
