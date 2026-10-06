# Swordfish Engine — Documentation Index

This is the subject index. Read this first. If you need to work on a subject, fetch
only the doc that covers it. Do not read the whole tree.

Every doc below records the exact contract, the source-of-truth code
(`file:line`), and the tests that pin the behavior. Stale docs are a bug: if a
change touches a subject, run the docs pass (see `development.md`).

## Subjects

| Subject | Doc |
|---|---|
| Workflows, toolchain, code style, commit rules, config schema, docs pass | [development](development.md) |
| As-built architecture, module map, persistence schema, CLI | [architecture](architecture.md) |
| Networking overview: process boundary, world split, current state | [specs/networking-overview](specs/networking-overview.md) |
| Wire message shapes (nsd schemas: snapshots, input, interaction event) | [specs/networking-messages](specs/networking-messages.md) |
| Stable component identity, direction, codecs | [specs/networking-registry](specs/networking-registry.md) |
| Transports, sessions, LAN discovery | [specs/networking-transports](specs/networking-transports.md) |
| Dirty-driven replication (server ↔ client) | [specs/networking-replication](specs/networking-replication.md) |
| Client prediction and reconciliation | [specs/networking-prediction](specs/networking-prediction.md) |
| Join handshake and full-world streaming | [specs/networking-join](specs/networking-join.md) |
| Server-authoritative interactions and voxel edits | [specs/networking-voxel-edits](specs/networking-voxel-edits.md) |
| Server-authoritative inventory moves | [specs/networking-inventory](specs/networking-inventory.md) |
| Multiple server worlds: per-world DI graphs, routing, idle unload | [specs/networking-worlds](specs/networking-worlds.md) |
| Persistence backends research (NATS vs sqlite, superseded) | [research/persistence-backends](research/persistence-backends.md) |
| Chat wire message, relay, logging, and UI | [specs/chat](specs/chat.md) |
| Persistence: SQLite layout, level databases, storage services | [specs/persistence](specs/persistence.md) |
| Asset definition TOML formats (items, bricks, materials, skills) | [specs/asset-definitions](specs/asset-definitions.md) |
| Brick identity, voxel ids, the palette, and data versioning | [specs/brick-identity](specs/brick-identity.md) |
| Reef rich-text color runs, the "#" grammar, and per-glyph colors | [specs/rich-text](specs/rich-text.md) |
| Server-authoritative skills: definitions, XP grant, skill notifications | [specs/skills](specs/skills.md) |
| Config schemas: `manifest.toml`, `modules.toml` | [specs/config-schemas](specs/config-schemas.md) |
| Local issue index and agent workflow | [specs/issues](specs/issues.md) |

## Conventions

- One subject per doc. A doc must be small and self-contained.
- Docs link to each other; they never duplicate content.
- The source of truth is always the code. Docs record behavior so you do not
  re-derive it, and point at the implementing `file:line`.
- All new doc prose uses Simplified Technical English: active voice, short
  sentences, no semicolons, no contractions, one name per thing.

## Live index

The live issue index is `docs/issues/index.md`. See
[specs/issues](specs/issues.md) for the format and workflow.