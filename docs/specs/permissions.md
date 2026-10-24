# Swordfish Engine — Permissions

One subject: the dot-key permission policy. It covers the file format, the load
and merge rules, the resolution rules, and the query API.

## Purpose

The permission policy answers one question: does this user have this permission?
Permissions are arbitrary dot keys, such as `waywardbeyond.level.save`. Feature
code defines the keys. Admins grant them in TOML files. A new group or a new
permission never needs a code change.

## File sources

The loader scans the module asset root `permissions/` through the virtual file
system, and the admin root `config/permissions/` on disk relative to the working
directory. Both roots are recursive. Only `.toml` files load. All files from
both roots merge into one policy.

The two roots are additive. A config file can grant a node, but it cannot remove
an asset grant, except with an explicit deny.

`PermissionFileLoader.Load`
(`WaywardBeyond.Permissions/PermissionFileLoader.cs:21`) sorts the paths before
parsing, so the load order is deterministic. A parse failure is recorded as a
diagnostic and skips the file. Startup continues. An empty asset root records a
warning, because the game always ships a baseline. The server logs the collected
diagnostics at startup.

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
`Roles`, and `Permissions` are optional. A name that contains whitespace or a
wildcard is rejected with a warning.

## Merge rules

- Same-named groups merge. `Inherits` and `Permissions` form unions.
- Same-named users merge. `Roles` and `Permissions` form unions.
- Duplicate nodes collapse.
- Every declared node is parsed once, at merge time. An invalid node records one
  warning and drops the node. Unreferenced groups are validated too.

## Resolution rules

A user resolves in two layers.

1. The group layer is the closure of the `default` group and every declared
   role. Groups inherit their parents transitively. Multiple inheritance is
   allowed.
2. The user layer is the user's own `Permissions`.

Lookup tries the user layer first: the exact node, then the longest matching
`prefix.*`, then `*`. A query the user layer cannot answer falls through to the
group layer. A user entry therefore overrides a group entry in every case.

- Within one layer, a deny wins for the same node.
- `-` negates one exact node. It does not affect descendants. Use `-prefix.*` to
  deny a subtree.
- `*` grants or denies every node.
- `prefix.*` matches every descendant of `prefix`, but not `prefix` itself.
- A wildcard is valid only as the final segment. A middle wildcard, such as
  `a.*.b`, is invalid and records a warning.

## Cycle safety

`PermissionCompiler.ResolveClosure`
(`WaywardBeyond.Permissions/PermissionCompiler.cs:186`) resolves inheritance with
depth-capped recursion. A chain may hold at most 64 groups. A cycle contributes
every acyclic edge. The compiler skips the edge that closes the cycle and
records one warning with the cycle path and the source files. The group count
per role set is capped at 256. A cap breach records one error and truncates the
branch. Truncation follows the sorted parent order, so an operator must fix the
graph rather than rely on which branch survived. Compilation always terminates
and never throws.

## Load and query

`PermissionPolicy.Create`
(`WaywardBeyond.Permissions/PermissionPolicy.cs:59`) merges, resolves, and
compiles at startup. Every user declared in a file compiles once. Every unlisted
user shares the compiled default set. `PermissionPolicy.DEFAULT_ROLE` is
`default`.

`PermissionSet.HasPermission`
(`WaywardBeyond.Permissions/PermissionSet.cs:38`) walks the query as a
`ReadOnlySpan<char>`. It uses cached alternate lookups on an exact map and a
wildcard map, and it falls back to the parent set. It does not allocate. Prefer
`PermissionPolicy.GetPermissions` when code checks many keys, so the user lookup
happens once.

## API

| Type | Role |
|---|---|
| `PermissionPolicy` | The compiled policy: `GetPermissions`, `HasPermission`, `Create`, `Diagnostics`, `GroupCount`, `UserCount` |
| `PermissionSet` | One compiled layer: `HasPermission`; `AllowAll` grants everything |
| `PermissionFileLoader` | Scans the two roots and parses files |
| `PermissionDiagnostics` | `Warnings` and `Errors` from load and compile |
| `PermissionsComposition` | `RegisterPermissions` wires the parser, the loader, and the policy |
| `UserClaim` | The user identity the client presents |
| `ServerPeer.Claim` | The pre-join claim carried on the peer |

## Server integration

`PermissionResolver`
(`WaywardBeyond.Server/Permissions/PermissionResolver.cs:14`) resolves a claim to
a set and owns the host rule. A local host connection gets
`PermissionSet.AllowAll`. `UserPermissionService` binds a session to a claim at
join and resolves the set once. `ServerLevelManager` asks the same resolver for
menu-time checks, so both paths always agree.

`ServerJoinSystem` reads `ServerPeer.Claim` at join and unbinds on leave or
disconnect. A peer with no hello joins anonymous.

`PermissionEntryPoint` resolves the policy at startup and logs the counts and
every diagnostic. `GamePermissions` declares the built-in feature keys.

`ServerHandlers.OnClientHello` stores the claim on `ServerPeer`. The client sends
`ClientHello { UserId }` once when the connection opens. The server ignores a
hello from a peer that already joined a world, and it rejects a user id longer
than 128 characters. Menu-time calls resolve the user without carrying an id on
every message. The claim lives for the connection. A reconnect gets a new peer
and a new hello.

The claim is not authenticated. Any client can present any user id. Treat the
claim as a hint until an authentication layer exists. See
[unauthenticated user claim](../issues/0064-unauthenticated-user-claim.md).

The game ships `WaywardBeyond.Server/assets/permissions/default.toml`. It
declares the `default` and `admin` groups. Mods ship defaults under their own
`assets/permissions/` root.

### Save authorization

`ServerSaveSystem` requires `GamePermissions.LEVEL_SAVE`
(`waywardbeyond.level.save`) for a client `SaveLevelAsync` call. A denied call
returns `RpcResult.Failure(GameRpcErrors.PERMISSION_DENIED, ...)` and sends a
targeted `notification.save.denied` toast. The server logs the denial at
information level. The server autosave does not need the permission. The stock
remote client never sends the call unless the menu offers it. This check
protects against a modified client.

### Create authorization

`ServerLevelManager` (`WaywardBeyond.Server/ServerLevelManager.cs`) requires
`GamePermissions.LEVEL_CREATE` (`waywardbeyond.level.create`) for a client
`CreateLevelAsync` call. The resolver grants a local loopback peer everything. A
remote peer is allowed only after its claim arrives and the key is granted. A
denied call returns `RpcResult.Failure(GameRpcErrors.PERMISSION_DENIED, ...)`
and logs at information level. It creates no data. The stock client hides the
"Create save" option when it lacks the key, so the check protects against a
modified client. The server also reports the same verdict in
`ListLevelsResponse.CanCreateSave`, which the client caches in
`GameSaveService.CanCreateSave` and uses to gate the menu button (`HomePage`).

### Delete authorization

`ServerHostCommands` requires `GamePermissions.LEVEL_DELETE`
(`waywardbeyond.level.delete`) for a client `DeleteLevelAsync` call. It asks
`ServerLevelManager.CanDelete`, so the delete check resolves through the same
`PermissionResolver` as create. The resolver grants a local loopback peer
everything. A denied call returns
`RpcResult.Failure(GameRpcErrors.PERMISSION_DENIED, ...)`, logs at information
level, and deletes nothing. The stock client does not know the verdict in
advance, so a denied client sees the failure from the call itself.

## Tests that pin this

- `Swordfish.Tests/Permissions/PermissionCompilerTests.cs` — merging, layering,
  deny precedence, cycles, caps, and diagnostics.
- `Swordfish.Tests/Permissions/PermissionSetTests.cs` — set semantics and the
  zero-allocation check.
- `Swordfish.Tests/Permissions/UserPermissionServiceTests.cs` — session binding,
  host override, deny on unbind.
- `Swordfish.Tests/Permissions/PermissionFileLoaderTests.cs` — root scanning and
  the empty-root warning.
