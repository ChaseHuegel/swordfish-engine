# Naming conventions

A living document. It records the naming conventions the repository uses today
and how they evolve over time. When a convention changes or a new one emerges,
update it in the change that introduces it. Keep one name per thing across
modules. Match a sibling module before choosing a name. The base style —
PascalCase, `_` private fields, `I` prefixes, type placement — lives in
[development](../development.md).

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

## ECS

| Suffix | Meaning |
|---|---|
| `<thing>System` | an ECS system (`EntitySystem`) |
| `<thing>Component` | an ECS component (`readonly struct : IDataComponent`) |