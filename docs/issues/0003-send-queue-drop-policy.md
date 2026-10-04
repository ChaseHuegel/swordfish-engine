# Bug: Send-queue drop policy silently discards protocol-critical frames

- Type: bug
- Status: open
- Workflow: ../specs/issues.md

## Problem

`TcpTransport.Send<T>` (`TcpTransport.cs:178-189`) enqueues every message
into one bounded FIFO (default 256 frames) and drops the oldest frame when
full. The docs (`networking-transports.md`) pin reliability and ordering at
the transport ("ordered/lossless by the transport"), but the drop policy
silently discards discrete, non-resendable messages that share the queue
with 60 Hz snapshots: `JoinAccept`, `WorldEntityAdd`,
`WorldStreamComplete`, `ChatMessage`, `VoxelEditMessage`,
`NotificationMessage`, `SkillStateUpdateMessage`. There is no per-type
priority, no per-type cap, and no recovery.

Senders ignore the `Result`: `NetworkReplicationSystem.PublishStage`
(`:132-148`), `ServerInteractionSystem.BroadcastEdit` (`:209-212`), and
`ServerChatSystem` (`:59`) never observe a failure, so drops are silent by
construction. A drop of a per-tick snapshot is harmless; a drop of a
control or state message is data loss.

Decision (locked): replace the single FIFO with a reliable-priority queue
that never evicts, alongside the existing per-tick (droppable) queue.
Drop-oldest applies only to per-tick snapshot traffic.

## Acceptance criteria

- [ ] Two send paths: a reliable-priority queue that never evicts a frame,
      and the existing bounded per-tick queue whose drop-oldest policy
      applies to per-tick snapshot traffic only. Control and state
      messages (join, world stream, chat, voxel edits, notifications,
      skill updates) go to the reliable queue.
- [ ] Defensive logging: warn when the per-tick queue drops a frame; error
      when the reliable queue exceeds a concern threshold (config-driven),
      re-logged at an interval (e.g. every 100 messages) while it stays
      over the threshold so the condition becomes visible without spamming
      the log.
- [ ] Senders of control and state messages observe and log a failed send
      instead of ignoring the `Result`.
- [ ] Test: fill the per-tick queue with snapshot frames to capacity, then
      send one control message per class; none is dropped. Test that
      per-tick drop-oldest evicts only snapshot frames.