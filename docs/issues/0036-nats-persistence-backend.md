# Research: Persistence backend re-evaluation — NATS for game saves, sqlite for character saves?

- Type: research
- Status: done
- Workflow: ../specs/issues.md

## Problem

Open question from the playtest notes: with the persistence layer running
through a NATS-backed `KeyValueStore` (`ServerComposition.cs:14-20`) over
an embedded NATS process (`PersistentNatsProcess`), is NATS still the right
backing for both stores? Lean for discussion: no for character saves
(sqlite instead - small records, transactional integrity, simplicity),
yes for game saves (efficient streaming of large world data). The answer
shapes #0022 (NATS lifecycle), #0035 (dedicated server depends on NATS),
and #0008 (per-world save lifetimes).

## Scope to research and answer

- Current layout and cost: the `levels` and `characters` buckets,
  `KeyValueStore`'s surface, the storage directory (`saves/`), how the
  embedded NATS server affects startup/memory/shutdown (#0022), and what
  a dedicated server (#0035) would need either way.
- Game saves: value of NATS-backed KV for world data vs file-backed
  storage - actual world sizes, read/write patterns (save frequency,
  full-world flush), what "streaming" gains in practice.
- Character saves: write patterns per character (frequency, size), the
  sqlite option - reuse of the existing `Swordfish.Integrations` SQL
  integration, transactionality, corruption behavior vs the current KV.
- Interactions: migration path with versioned data, whether the engine's
  `KeyValueStore` abstraction can host both backends or a single backend
  should replace it, impact on tests/CI, and how #0008's per-world
  lifecycles and #0035's launcher are affected by the choice.

## Acceptance criteria

- [x] The evaluation and proposal are written to an **entirely new,
      standalone document** for user review - no existing spec,
      issue, or note is modified by this research's output.
- [x] The proposal contains measured data where possible (bucket sizes,
      write frequencies), a per-store recommendation (NATS vs sqlite vs
      file-backed), and the migration path.
- [x] The proposal is delivered to the user for review and approval;
      changes to other documents and issues (#0022/#0035/#0008) happen
      only after that approval, as separate work.