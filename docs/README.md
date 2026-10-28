# Swordfish Engine — Documentation Index

This is the subject index. Read this first. If you need to work on a subject, fetch
only the doc that covers it. Do not read the whole tree.

Every doc below records the exact contract, the source-of-truth code
(`file:line`), and the tests that pin the behavior. Stale docs are a bug: if a
change touches a subject, run the docs pass (see `development.md`).

## Subjects

| Subject | Doc |
|---|---|
| Principles, workflows, toolchain, code style, commit rules, config schema, docs pass | [development](development.md) |
| As-built architecture, module map, persistence schema, CLI | [architecture](architecture.md) |
| DI composition: who registers what, call sites, policies | [specs/composition](specs/composition.md) |
| Networking overview: process boundary, world split, current state | [specs/networking-overview](specs/networking-overview.md) |
| Needlefish codegen: schema layout, includes, generator options | [specs/needlefish-codegen](specs/needlefish-codegen.md) |
| Wire message shapes (nsd schemas: snapshots, input, interaction event) | [specs/networking-messages](specs/networking-messages.md) |
| nsd-rpc service catalogue, method ids, error codes, fragments, loopback | [specs/networking-rpc](specs/networking-rpc.md) |
| Stable component identity, direction, codecs | [specs/networking-registry](specs/networking-registry.md) |
| Transports, sessions, LAN discovery | [specs/networking-transports](specs/networking-transports.md) |
| Dirty-driven replication (server ↔ client) | [specs/networking-replication](specs/networking-replication.md) |
| Client prediction and reconciliation | [specs/networking-prediction](specs/networking-prediction.md) |
| Join handshake and full-world streaming | [specs/networking-join](specs/networking-join.md) |
| Server-authoritative interactions and voxel edits | [specs/networking-voxel-edits](specs/networking-voxel-edits.md) |
| Interaction pipeline: stages, priority, decisions, handler composition | [specs/interaction-pipeline](specs/interaction-pipeline.md) |
| Server-authoritative inventory moves | [specs/networking-inventory](specs/networking-inventory.md) |
| Multiple server worlds: per-world DI graphs, routing, idle unload | [specs/networking-worlds](specs/networking-worlds.md) |
| Lua scripting proposal (not implemented) | [proposals/scripting-lua](proposals/scripting-lua.md) |
| Dynamic shadows: Forward+ CSM, point cubemap arrays, filtering, quality presets | [specs/rendering-shadows](specs/rendering-shadows.md) |
| Chat wire message, relay, logging, and UI | [specs/chat](specs/chat.md) |
| Persistence: SQLite layout, level databases, storage services | [specs/persistence](specs/persistence.md) |
| Seed phrase generation, 40-bit dictionary encoding, and 64-bit seed resolution | [specs/seed-phrases](specs/seed-phrases.md) |
| Asset definition TOML formats (items, bricks, materials, skills) | [specs/asset-definitions](specs/asset-definitions.md) |
| Brick identity, voxel ids, the palette, and data versioning | [specs/brick-identity](specs/brick-identity.md) |
| Voxel container: chunk addressing, sampling, out-of-bounds, palette counts | [specs/voxels](specs/voxels.md) |
| Native resource lifetime: GPU handle and Jolt shape disposal | [specs/resource-lifetime](specs/resource-lifetime.md) |
| Reef rich-text color runs, the "#" grammar, and per-glyph colors | [specs/rich-text](specs/rich-text.md) |
| Reef element layout, styling, borders, no-wrap text, clipping | [specs/reef-ui](specs/reef-ui.md) |
| Reef markup language (.rml) and stylesheets (.rss) | [specs/reef-markup](specs/reef-markup.md) |
| Reef markup generator architecture and implementation blueprint | [specs/reef-markup-blueprint](specs/reef-markup-blueprint.md) |
| Reef markup implementation plan, locked decisions, and diagnostics | [specs/reef-markup-implementation-plan](specs/reef-markup-implementation-plan.md) |
| Reef markup onboarding, examples, quick reference, and test traceability | [userdocs/reef](../userdocs/reef/README.md) |
| Game UI theme tokens, metrics, style resolution, and shared widgets | [specs/ui-theme](specs/ui-theme.md) |
| Main menu flow, menu layout, data mapping, and visual rules | [specs/main-menu-ui](specs/main-menu-ui.md) |
| UI copy style, catalog conventions, key grammar, vocabulary, and string mappings | [specs/ui-copy](specs/ui-copy.md) |
| Full-screen loading layer, visual rules, copy timing, data sources | [specs/loading-screen](specs/loading-screen.md) |
| Window video modes and batched display changes | [specs/window-display](specs/window-display.md) |
| Settings page layout, controls, draft, and apply behavior | [specs/settings-ui](specs/settings-ui.md) |
| Server-authoritative skills: definitions, XP grant, skill notifications | [specs/skills](specs/skills.md) |
| Dot-key permissions: file format, merge, resolution, zero-allocation policy | [specs/permissions](specs/permissions.md) |
| Config schemas: `manifest.toml`, `modules.toml` | [specs/config-schemas](specs/config-schemas.md) |
| Self-review and peer review: triage, hot paths, threading, networking, gate | [specs/code-review](specs/code-review.md) |
| Naming conventions common type patterns | [specs/naming](specs/naming.md) |
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