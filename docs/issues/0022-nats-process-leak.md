# Bug: Embedded NATS server process leaks on app close

- Type: bug
- Status: open
- Workflow: ../specs/issues.md

## Problem

Playtest: the embedded NATS server is not shut down when the app closes.
`PersistentNatsProcess.Dispose` (`PersistentNatsProcess.cs:62-68`) only
calls `_process?.Dispose()` - disposing a `Process` object releases the
handle without terminating the child, so the `nats-server` process
survives on Linux, and on Windows the Job object does not terminate
children unless the kill-on-close limit is set.

Two further hazards:

- The `OnProcessExited` handler restarts the child on crash
  (`PersistentNatsProcess.cs:137-170`) and is not detached or guarded
  during dispose, so teardown can be followed by a resurrected server.
- The close path itself must be verified to reach `Entry.Dispose`
  (`Entry.cs:81-84`) via container teardown on window close.

NATS stays load-bearing (NATS-backed `KeyValueStore` persistence - see the
#0016 re-scope), so the child must be stopped deterministically.

## Acceptance criteria

- [ ] `PersistentNatsProcess.Dispose` terminates the child process
      deterministically on both platforms (kill + bounded wait; no
      reliance on `Process.Dispose`), and detaches the `Exited` and
      output handlers so no restart can occur during or after dispose.
- [ ] The application close path is verified to reach
      `PersistentNatsProcess.Dispose` (window close -> container teardown
      -> `Entry.Dispose`); a missed teardown path is fixed.
- [ ] Test/manual verification: quit the app with an active NATS child and
      confirm no `nats-server` process remains on Linux and Windows.
- [ ] `docs/specs/persistence.md` notes the embedded-process lifecycle
      (start, restart-on-crash, dispose) if not already present (docs
      pass).