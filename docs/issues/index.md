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

- [x] [Authoritative snapshot deltas are droppable](/docs/issues/0045-snapshot-delta-loss.md) — drop-oldest loses removals, one-shot updates, and client commands. Snapshots must ride the reliable path.


### Improvement
- [x] [Add the nsd-rpc protocol layer and loopback transport](/docs/issues/0046-nsd-rpc-protocol-layer.md) — services, method ids, fragments, loopback pair, RPC logging bridge
- [x] [Migrate the server to nsd-rpc services](/docs/issues/0047-migrate-server-to-rpc.md) — peer directory, deferred handlers, two-call join, paced level stream, reliable snapshots
- [x] [Migrate the client to nsd-rpc services](/docs/issues/0048-migrate-client-to-rpc.md) — client host, inbound staging, two-call join, typed level calls
- [x] [Restore the loopback session after a remote disconnect](/docs/issues/0049-restore-loopback-after-remote.md) — a remote drop leaves no active connection; local play needs an application restart
- [x] [Remove the legacy transport and remap network config](/docs/issues/0050-remove-legacy-transport.md) — delete the old TCP stack, serializers, hub, and keys; bump the discovery protocol
- [x] [Add the RPC migration test suite and networking docs pass](/docs/issues/0051-rpc-tests-and-docs.md) — pin fragmentation, join, backlog, reconnect, and rewrite the networking specs

### Research


## Backlog

### Bug

- [ ] [DataStore.Free can double-free an entity index](/docs/issues/0039-datastore-free-idempotency.md) — a second Free re-enqueues the index; one index can reach two owners (engine-first)
- [x] [Idle worlds replicate physics state every snapshot](/docs/issues/0053-idle-replication-traffic.md) — the Jolt sync re-dirties unchanged Transform/Physics and resets sleep; idle input uploads every tick
- [ ] [nsd-rpc keeps a canceled inbound entry with a disposed token source](/docs/issues/0052-nsd-rpc-canceled-inbound-dispose.md) — a canceled call makes the next disconnect throw (upstream)
- [ ] [CharacterSaveManagerTests session-time test never sets the active save](/docs/issues/0041-character-save-load-test.md) — Load returns failure; the test asserts success
- [ ] [CLI parser splits quoted multi-word values](/docs/issues/0043-cli-quoted-value-split.md) — `--name "Smoke Test"` parses to `['Smoke', 'Test']`; consumers take only `values[0]`
- [ ] [Bound how old a resolved input command may be](/docs/issues/0059-bound-command-staleness.md) — a client that stops sending keeps moving; the step re-applies its last sample forever
- [ ] [Transport tests flake when the suite runs in parallel](/docs/issues/0063-flaky-transport-tests.md) — two loopback socket tests fail intermittently only under the full suite
- [ ] [The user claim is unauthenticated](/docs/issues/0064-unauthenticated-user-claim.md) — any client can present any user id, so a GUID's secrecy is the whole authorization model

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

### Research
