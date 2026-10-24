# Issue Index

One subject: the live index of open, in-progress, and completed issues.
Read [specs/issues](../specs/issues.md) before adding or changing an issue.

Two top-level sections hold the work: [Active Sprint](#active-sprint) and
[Backlog](#backlog). Each has three type subsections: Bug, Improvement, and
Research.

Each issue appears as a checkbox bullet linking to its own document. A pending
issue is unchecked (`- [ ]`). A done issue is checked (`- [x]`). A done issue
stays in its subsection, checked in place. Sprint rotation is user-managed and
is not part of this workflow.

## Active Sprint

### Bug

### Improvement

### Research

## Backlog

### Bug

- [ ] [DataStore.Free can double-free an entity index](/docs/issues/0039-datastore-free-idempotency.md) — a second Free re-enqueues the index; one index can reach two owners (engine-first)
- [ ] [nsd-rpc keeps a canceled inbound entry with a disposed token source](/docs/issues/0052-nsd-rpc-canceled-inbound-dispose.md) — a canceled call makes the next disconnect throw (upstream)
- [ ] [CharacterSaveManagerTests session-time test never sets the active save](/docs/issues/0041-character-save-load-test.md) — Load returns failure; the test asserts success
- [ ] [CLI parser splits quoted multi-word values](/docs/issues/0043-cli-quoted-value-split.md) — `--name "Smoke Test"` parses to `['Smoke', 'Test']`; consumers take only `values[0]`
- [ ] [Bound how old a resolved input command may be](/docs/issues/0059-bound-command-staleness.md) — a client that stops sending keeps moving; the step re-applies its last sample forever
- [ ] [Transport tests flake when the suite runs in parallel](/docs/issues/0063-flaky-transport-tests.md) — two loopback socket tests fail intermittently only under the full suite
- [ ] [The server integration test writes the store off the server thread](/docs/issues/0082-test-writes-store-off-thread.md) — seeding a live world from the test thread races the `"Server"` tick
- [ ] [The user claim is unauthenticated](/docs/issues/0064-unauthenticated-user-claim.md) — any client can present any user id, so a GUID's secrecy is the whole authorization model
- [ ] [Voxel palette count means two different things](/docs/issues/0070-voxel-palette-count-semantics.md) — `Count()` returns distinct ids, so the empty-chunk guard never fires and each empty chunk asks for 4095 spare slots
- [ ] [The voxel container leaks live voxel arrays](/docs/issues/0071-voxel-container-leaks-live-arrays.md) — `VoxelCodec.Encode` still reuses the caller's chunk arrays
- [ ] [The two voxel sample producers disagree](/docs/issues/0072-voxel-sample-producers-disagree.md) — one rotates neighbours by voxel orientation and one does not; one leaves the coordinate fields at zero
- [ ] [Voxel chunks are unbounded and chunk coordinates truncate](/docs/issues/0073-unbounded-voxel-chunks.md) — a `short` chunk cast wraps a distant write into a resident chunk, and `_chunks` never shrinks
- [x] [Voxel edits leak mesh renderers and rebuild collision shapes](/docs/issues/0083-voxel-edit-memory-leak.md) — the cleanup gate never runs, so every edit leaks a mesh and a render target, and the Jolt compound is rebuilt without deterministic shape disposal ([#612](https://github.com/ChaseHuegel/swordfish-engine/issues/612))

### Improvement

- [ ] [nsdc unpack safety — submit B1-B3 upstream](/docs/issues/0037-nsdc-unpack-safety.md) — generated deserializer OOB reads; frame-level mitigations landed, structural fix needs codegen
- [ ] [Move character tunables into config](/docs/issues/0054-move-character-tunables-to-config.md) — movement, reach, and hotbar constants are hardcoded and absent from the config schema
- [ ] [Remove the TODO markers from WaywardBeyond.Gameplay](/docs/issues/0055-remove-gameplay-todos.md) — two new TODO comments duplicate issues 0054 and 0056
- [ ] [Split CharacterDefaults by concern](/docs/issues/0056-split-character-defaults.md) — movement, reach, inventory, and body geometry in one type; two competing heights
- [ ] [Make the NetworkComponent invariants real](/docs/issues/0057-network-component-invariants.md) — nullable staging rings, and a Session handle that is written but never read
- [ ] [Store the last applied look in a component](/docs/issues/0058-look-history-component.md) — per-entity sim state lives in a side dictionary with a hand-written prune
- [ ] [Clamp item stack capacity on insert](/docs/issues/0060-clamp-item-stack-capacity.md) — a zero MaxSize stack enters an empty slot and can never merge again
- [ ] [Remove per-interaction allocations in the server interaction path](/docs/issues/0061-interaction-hot-path-allocations.md) — a voxel world and capturing lambdas per interaction edge
- [ ] [Move game tests out of the engine test project](/docs/issues/0062-game-test-project.md) — game tests in Swordfish.Tests make game work an engine-project change
- [ ] [Inspect effective permissions at runtime](/docs/issues/0065-runtime-permission-inspection.md) — no host command can print a user's set or explain a denial
- [ ] [Make ClientHello a request/response call](/docs/issues/0066-client-hello-request-response.md) — a one-way hello silently leaves the peer on the default set and cannot carry session information
- [ ] [Reload the permission policy without a restart](/docs/issues/0067-permission-policy-reload.md) — the policy is compile-once, and bound sessions cache their set at join
- [ ] [Add a skill panel that consumes skill icons and categories](/docs/issues/0068-skill-panel.md) — `Skill.Icon` and `Skill.Category` have no reader, and icon paths are unvalidated
- [ ] [Make one list decide each brick property](/docs/issues/0069-brick-tags-source-of-truth.md) — brick tags and the skill tag lists duplicate each other, disagree, and both drive gameplay
- [ ] [One chunk layout decides voxel indexing](/docs/issues/0074-single-chunk-layout.md) — four copies of the index math have already diverged, and `VoxelChunkWriter` duplicates the shared container
- [ ] [The voxel palette locks on a dictionary](/docs/issues/0075-voxel-palette-lock-target.md) — `lock` on a field with meaningful identity, and nothing contends for it
- [ ] [VoxelCodec reports failure through Result](/docs/issues/0076-voxel-codec-result.md) — a skipped encode and a structural failure look the same to all four call sites
- [ ] [Preserve unknown bricks across a save round trip](/docs/issues/0077-preserve-unknown-bricks.md) — an unknown palette name becomes the empty voxel, so a modded save loaded in vanilla is destroyed on re-save
- [ ] [Delete dead voxel members and state container ownership](/docs/issues/0078-dead-voxel-members.md) — unused `Orientation` members and no stated owner for the container a component holds
- [ ] [Document the voxel public surface](/docs/issues/0079-document-voxel-surface.md) — the new shared assembly has almost no contract docs and no `<inheritdoc/>`
- [ ] [Encapsulate VoxelObject internals](/docs/issues/0080-encapsulate-voxel-object.md) — `ChunkData` re-derives index math from raw internal fields, under a `TODO`
- [ ] [Harden the voxel module](/docs/issues/0081-voxels-tech-debt.md) — unmasked `ShapeLight` nibbles, a heavyweight transitive `Data` dependency, and an undocumented angle unit
- [ ] [Unify the game data version](/docs/issues/0084-unify-data-version.md) — the server catalog hardcodes a `Data.Version` that duplicates `WaywardBeyond.Version` and can drift
- [ ] [nsd-rpc notification proxies swallow the send result](/docs/issues/0085-notification-proxy-send-result.md) — the generated proxy discards `RpcPeer.Notify`, so the server bypasses it to detect a rejected send
- [ ] [Reduce the cost of a voxel edit rebuild](/docs/issues/0086-incremental-voxel-rebuild.md) — every edit remeshes the whole structure, collides every solid voxel, and draws unseen structures

### Research
