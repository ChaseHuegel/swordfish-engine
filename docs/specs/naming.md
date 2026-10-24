# Naming conventions

A living document. It records the naming conventions the repository uses today
and how they evolve over time. When a convention changes or a new one emerges,
update it in the change that introduces it. Keep one name per thing across
modules. Match a sibling module before choosing a name. The base style —
PascalCase, `_` private fields, `I` prefixes, type file names, and type
placement — lives in [development](../development.md).

The sections below record the current conventions. They grow as the repository
grows. This document should be updated as conventions are established or arise.

## Asset pipeline

| Suffix | Meaning |
|---|---|
| `<thing>Definition` | the asset row model, parsed from a file |
| `<thing>Definitions` | the collection / file model for a parsed asset resource |
| `<thing>Database` | the store that loads and owns the assets (a `VirtualAssetDatabase`) |
| `<thing>` | the loaded runtime asset the database returns |

Schema classes carry the asset kind: `BrickDefinition`, `BrickDefinitions`,
`BrickDatabase`. The runtime asset a database returns is named for the kind
itself, not a view: `Brick`, `Body`, `Skill`, `Item`, `Material`. Prefix the
database name with `I` only when a cross-assembly or polymorphic contract is
needed: `IBrickDatabase`.

## Config

| Suffix | Meaning |
|---|---|
| `<thing>Config` | a TOML-backed config model |

One config model per subject. The base class is `Config<T>` in
`Swordfish.Library`.

## DI composition extensions

Composition helpers register a project's services through extension methods on
`IContainer`. The owning project hosts the helpers next to its API types.

| Pattern | Meaning |
|---|---|
| `<Project>Composition` | the static class hosting a project's composition extensions |
| `Register<Thing>(this IContainer)` | an extension method that registers one concern |

Examples: `NetworkingComposition` hosts `RegisterNetworking`.
`DataComposition` hosts `RegisterDataServices`.
`LocalizationComposition` hosts `RegisterTags`.
`PermissionsComposition` hosts `RegisterPermissions`.

A sub-area within a project hosts its own class in its namespace. Example:
`WaywardBeyond.Data.Localization.LocalizationComposition`.

Call sites and ownership rules live in [composition](composition.md).

## ECS

| Suffix | Meaning |
|---|---|
| `<thing>System` | an ECS system (`EntitySystem`) |
| `<thing>Component` | an ECS component (`readonly struct : IDataComponent`) |

## Interaction handlers

An interaction handler's suffix names the pipeline stage it runs in. The subject names
the content or action it handles.

| Suffix | Stage |
|---|---|
| `<thing>Selector` | `Select` — chooses the content the interaction operates with |
| `<thing>Resolver` | `Resolve` — derives the base outcome from the client's target hint |
| `<thing>Validator` | `Validate` — adjusts or rejects before the authority change |
| `<thing>Committer` | `Commit` — performs the authority change |
| `<thing>Reactor` | `React` — applies the consequences |

Examples: `ItemPlaceableSelector`, `HintResolver`, `VoxelWriteCommitter`,
`BreakLootReactor`, `SkillXpReactor`.

A handler declares its `Stage` and `Priority` as properties rather than at registration,
so the type is the one place that says where it runs. Handlers register as
`IInteractionHandler` through DI. See [interaction-pipeline](interaction-pipeline.md).

## Storage

The persistence layer returns models.
The model is named for the domain, not its current content.

| Suffix | Meaning |
|---|---|
| `I<thing>Storage` | persistence contract over one database |
| `Sqlite<thing>Storage` | the SQLite implementation of the contract |
| `<thing>Storage` model | named for the domain it stores, not one of its fields |
