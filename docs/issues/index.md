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

- [ ] [Authoritative snapshot deltas are droppable](/docs/issues/0045-snapshot-delta-loss.md) — drop-oldest loses removals, one-shot updates, and client commands. Snapshots must ride the reliable path.


### Improvement
- [ ] [Add the nsd-rpc protocol layer and loopback transport](/docs/issues/0046-nsd-rpc-protocol-layer.md) — services, method ids, fragments, loopback pair, RPC logging bridge
- [ ] [Migrate the server to nsd-rpc services](/docs/issues/0047-migrate-server-to-rpc.md) — peer directory, deferred handlers, two-call join, paced level stream, reliable snapshots
- [ ] [Migrate the client to nsd-rpc services](/docs/issues/0048-migrate-client-to-rpc.md) — client host, inbound staging, two-call join, typed level calls
- [ ] [Restore the loopback session after a remote disconnect](/docs/issues/0049-restore-loopback-after-remote.md) — a remote drop leaves no active connection; local play needs an application restart
- [ ] [Remove the legacy transport and remap network config](/docs/issues/0050-remove-legacy-transport.md) — delete the old TCP stack, serializers, hub, and keys; bump the discovery protocol
- [ ] [Add the RPC migration test suite and networking docs pass](/docs/issues/0051-rpc-tests-and-docs.md) — pin fragmentation, join, backlog, reconnect, and rewrite the networking specs

### Research


## Backlog

### Bug

- [ ] [DataStore.Free can double-free an entity index](/docs/issues/0039-datastore-free-idempotency.md) — a second Free re-enqueues the index; one index can reach two owners (engine-first)
- [ ] [CharacterSaveManagerTests session-time test never sets the active save](/docs/issues/0041-character-save-load-test.md) — Load returns failure; the test asserts success
- [ ] [CLI parser splits quoted multi-word values](/docs/issues/0043-cli-quoted-value-split.md) — `--name "Smoke Test"` parses to `['Smoke', 'Test']`; consumers take only `values[0]`

### Improvement

- [ ] [nsdc unpack safety — submit B1-B3 upstream](/docs/issues/0037-nsdc-unpack-safety.md) — generated deserializer OOB reads; frame-level mitigations landed, structural fix needs codegen

### Research
