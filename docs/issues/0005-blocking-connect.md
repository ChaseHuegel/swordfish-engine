# Bug: Blocking, timeout-less TCP connect hangs the client

- Type: bug
- Status: done
- Workflow: ../specs/issues.md

## Problem

`TcpTransport.Connect` (`TcpTransport.cs:99`) is a synchronous
`_client.Connect(host, port)` with no connect timeout, called from
`TransportManager.ConnectRemote` (`TransportManager.cs:64`) on the
multiplayer UI/ECS path. Against an unreachable host (dead LAN IP, dropped
SYNs, blackholed route) the blocking connect waits out the OS TCP retry
schedule - tens of seconds on Linux - freezing the game with no cancel and
no feedback.

## Acceptance criteria

- [x] Connect is bounded by `ConnectionTimeoutMs`: an
      unroutable/blackholed host fails within the configured bound, and
      the calling thread never blocks past it.
- [x] Test: connect to a blackholed address returns a failure `Result`
      within `ConnectionTimeoutMs` and the transport is left cleanly
      disposed.
- [x] The UI path issuing a connect stays responsive for the duration of
      the attempt.