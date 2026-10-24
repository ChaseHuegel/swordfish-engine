# Bug: Shared NATS server never promotes when its owner closes

- Type: bug
- Status: done
- Workflow: ../specs/issues.md

## Problem

`PersistentNatsProcess` always starts a child on the default port with
`-sd saves/` and never checks for an existing server
(`PersistentNatsProcess.cs:67`). A second process on the same machine fails to
bind with `[FTL] address already in use` yet continues, and its
`KeyValueStore` silently connects to the first process's server
(`KeyValueStore.cs:28`, default `nats://127.0.0.1:4222`). `PersistentNatsProcess`
ignores `NATS_URL`, so owner and clients can disagree on the address.

`OnProcessExited` restarts the child unconditionally and throws when the
restarted child exits immediately (`PersistentNatsProcess.cs:208`). The
exception kills the restart path, so the second process never takes over after
the first closes. The child is also killed with SIGKILL on Linux
(`PersistentNatsProcess.cs:101`), which forces JetStream recovery warnings
(`Stream state outdated ... will rebuild`).

Multiple processes sharing one NATS server is the intended model. Sharing the
client-owned `saves` and `characters` buckets across processes is accepted.

## Acceptance criteria

- [x] At boot the process probes the NATS address. A reachable address selects
      shared mode with no child; otherwise it starts the child as owner.
- [x] In shared mode, when the address becomes unreachable the process starts
      the child with backoff, and stops probing once it binds.
- [x] An owned child that exits restarts with backoff and never throws out of
      the exit handler.
- [x] The child launches with the host and port from `NATS_URL`, and Linux stops
      it gracefully before kill.
- [x] `KeyValueStore` retries the initial connect (`RetryOnInitialConnect`).
- [x] Tests cover shared-mode detection and promotion.
