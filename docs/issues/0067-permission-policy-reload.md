# Improvement: Reload the permission policy without a restart

- Type: improvement
- Status: open
- Workflow: ../specs/issues.md

## Problem

`PermissionPolicy` compiles at startup and is immutable afterwards.
`PermissionsComposition.RegisterPermissions` registers it as a singleton
delegate. An admin who edits `config/permissions/` must restart the server.

Bound sessions also cache their `PermissionSet` at join, so a reload has to
rebind or invalidate every binding. Without that step a reload would apply to
menus but not to worlds.

## Acceptance criteria

- [ ] An operator can reload the policy from disk without a restart.
- [ ] A reload rebinds or invalidates every bound session set.
- [ ] A failed parse keeps the previous policy and logs the new diagnostics.
- [ ] The policy is swapped atomically and is never observed half-built.
- [ ] `docs/specs/permissions.md` describes the trigger and the guarantees.
