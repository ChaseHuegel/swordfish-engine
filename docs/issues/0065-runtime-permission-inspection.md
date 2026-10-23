# Improvement: Inspect effective permissions at runtime

- Type: improvement
- Status: open
- Workflow: ../specs/issues.md

## Problem

The only view of a compiled policy is the startup log line and the collected
diagnostics (`WaywardBeyond.Server/Permissions/PermissionEntryPoint.cs`). An
operator cannot ask the running server for a user's effective set, and cannot
find out which layer decided a key.

`ServerHostCommands` already carries host commands, and `PermissionPolicy`
exposes no enumeration. The denial logs name the key but not the deciding layer.

## Acceptance criteria

- [ ] A host command prints a user's effective nodes, grouped by layer.
- [ ] A host command explains one key for one user, naming the layer and node
      that decided it.
- [ ] Output is bounded or paginated for a large policy.
- [ ] `docs/specs/permissions.md` describes the commands.
