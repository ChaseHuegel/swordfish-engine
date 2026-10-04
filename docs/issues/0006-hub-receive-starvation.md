# Bug: Hub receive poll lets one chatty client starve the rest

- Type: bug
- Status: open
- Workflow: ../specs/issues.md

## Problem

`ServerConnectionHub.Receive<T>` (`ServerConnectionHub.cs:60-70`) loops
`while (result.Success)` per connection inside a single poll. A client
flooding frames (cheap to craft, queued unbounded per type by the
transport) monopolizes the entire drain on the server tick - `ApplyStage`
never yields, so other clients are not sampled that tick and their
commands go unprocessed. Round-robin fairness between clients is absent.

The method's doc comment claims "the connection list is snapshotted so a
client removed mid-poll is not enumerated"; `ConcurrentDictionary`
enumeration is weakly consistent, so the comment is wrong on both counts.

Decision (locked): per-client drain is bounded by a new
`NetworkingSettings.MaxReceiveWindow` key (default 10), so every client is
serviced at a fair pace within each poll.

## Acceptance criteria

- [ ] Each poll drains at most `MaxReceiveWindow` frames per client
      (default 10) before moving to the next connection. Config key
      documented in the `networking-transports.md` table.
- [ ] Comment corrected: per-connection polling is weakly consistent and
      bounded per client; removal mid-poll is safe for the dictionary but
      must not be described as a snapshot.
- [ ] Test: one client queueing 1000+ frames does not prevent a second
      client's frames from being received within the same poll (or across
      two polls).