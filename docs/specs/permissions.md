# Swordfish Engine — Permissions

One subject: the dot-key permission policy. It covers the file format, the load
and merge rules, the resolution rules, and the query API.

## Purpose

The permission policy answers one question: does this user have this permission?
Permissions are arbitrary dot keys, such as `waywardbeyond.level.save`. Feature
code defines the keys. Admins grant them in TOML files. A new group or a new
permission never needs a code change.

## File sources

The loader scans two roots. It scans the module asset root `permissions/` first,
then the admin root `config/permissions/`. Both roots are recursive. Only `.toml`
files load. All files merge into one policy.

`PermissionFileLoader.Load`
(`WaywardBeyond.Shared.Permissions/PermissionFileLoader.cs:33`) sorts the paths
before parsing, so the load order is deterministic. A parse failure logs an error
and skips the file. Startup continues.

## File format

A file declares groups and users. The same group name or user id can appear in
many files.

```toml
[Groups.builder]
Inherits = ["default"]
Permissions = ["waywardbeyond.brick.place", "waywardbeyond.brick.break"]

[Groups.moderator]
Inherits = ["builder"]
Permissions = ["waywardbeyond.level.save"]

[Users."3f2504e0-4f89-11d3-9a0c-0305e82c3301"]
Roles = ["moderator"]
Permissions = ["-waywardbeyond.brick.break"]
```

Group names, user ids, and permission keys are case-insensitive. `Inherits`,
`Roles`, and `Permissions` are optional.

## Merge rules

- Same-named groups merge. `Inherits` and `Permissions` form unions.
- Same-named users merge. `Roles` and `Permissions` form unions.
- Duplicate nodes collapse.
- A grant and a deny for the same node in different files resolve to deny.

## Resolution rules

- Every user belongs to the `default` group, if that group exists.
- A user also belongs to every declared role.
- Groups inherit their parents transitively. Multiple inheritance is allowed.
- The effective set is the union of all group nodes and the user's own nodes.
- `-` negates a node. Deny wins for the same node.
- `*` grants every node. `prefix.*` grants every descendant of `prefix`, but not
  `prefix` itself.
- A wildcard is valid only as the final segment. A middle wildcard, such as
  `a.*.b`, is invalid and raises a warning.
- Lookup precedence is the exact node, then the longest matching `prefix.*`,
  then `*`.

## Cycle safety

`PermissionCompiler.ResolveClosure`
(`WaywardBeyond.Shared.Permissions/PermissionCompiler.cs:178`) resolves
inheritance with an explicit stack. It never recurses. A cycle contributes every
acyclic edge. The compiler skips the edge that closes the cycle and records one
warning with the cycle path and the source files. Depth is capped at 64. The
group count per user is capped at 256. A cap breach logs an error and truncates
the branch. Compilation always terminates and never throws.

## Load and query

`PermissionPolicy.Create`
(`WaywardBeyond.Shared.Permissions/PermissionPolicy.cs:73`) merges, resolves,
and compiles at startup. Every user declared in a file compiles once. Every
unlisted user shares the compiled default set.
`PermissionPolicy.DEFAULT_ROLE` is `default`.

`PermissionSet.HasPermission`
(`WaywardBeyond.Shared.Permissions/PermissionSet.cs:56`) walks the query as a
`ReadOnlySpan<char>`. It uses cached alternate lookups on an exact map and a
wildcard map. It does not allocate. Prefer `PermissionPolicy.GetPermissions`
when code checks many keys, so the user lookup happens once.

## API

| Type | Role |
|---|---|
| `IPermissionPolicy` | `GetPermissions`, `HasPermission`, `GetRoles`, `GetEffectivePermissions`; `Diagnostics`, `GroupCount`, `UserCount` |
| `PermissionSet` | A compiled set; `HasPermission`; `AllowAll` grants everything |
| `PermissionPolicy` | The compiled policy; `Create` |
| `PermissionFileLoader` | Scans the two roots and parses files |
| `PermissionLoadOptions` | `AssetRoot` (default `permissions/`), `ConfigRoot` (default `config/permissions/`) |
| `PermissionDiagnostics` | `Warnings` and `Errors` from load and compile |
| `UserClaim`, `IUserClaimProvider` | The user identity seam |

## Server integration

`UserPermissionService` (`WaywardBeyond.Server.Core/Permissions/UserPermissionService.cs`)
binds a session to a claim and resolves the compiled set once at bind time.
`ServerJoinSystem` binds at join and unbinds on leave or disconnect. The local
host connection gets `PermissionSet.AllowAll`. An unbound client is denied.

`PermissionEntryPoint`
(`WaywardBeyond.Server.Core/Permissions/PermissionEntryPoint.cs`) resolves the
policy at startup and logs the counts and every diagnostic.
`GamePermissions` (`WaywardBeyond.Server.Core/Permissions/GamePermissions.cs`)
declares the built-in feature keys.

The game ships `WaywardBeyond.Server.Core/assets/permissions/default.toml`. It
declares the `default` and `admin` groups. Mods ship defaults under their own
`assets/permissions/` root.

## Tests that pin this

- `Swordfish.Tests/Permissions/PermissionCompilerTests.cs` — merge, inheritance,
  deny and wildcard precedence, cycle handling, caps, invalid nodes.
- `Swordfish.Tests/Permissions/PermissionSetTests.cs` — set semantics and the
  zero-allocation check.
- `Swordfish.Tests/Permissions/PermissionPolicyTests.cs` — default set sharing.
- `Swordfish.Tests/Permissions/PermissionFileLoaderTests.cs` — asset and config
  roots, parse-failure skip.
