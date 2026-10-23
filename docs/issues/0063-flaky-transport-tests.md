# Bug: Transport tests flake when the suite runs in parallel

- Type: bug
- Status: open
- Workflow: ../specs/issues.md

## Problem

Two transport tests fail intermittently when the whole `Swordfish.Tests` suite
runs, and pass when run alone:

- `ServerRpcTests.TcpMenuCallsUseTheLanEndpointAndDenyRemoteCreate`
- `TransportPolicyTests.StalledPeerIsClosedWithBacklogLimit`

Both bind or connect real sockets on the loopback interface. The failure rate
grew as the suite grew, which points to contention between parallel test
collections over ports or thread scheduling rather than a logic defect.

A red build from an unrelated change is the cost. The gate cannot be trusted when
the suite is not stable.

## Acceptance criteria

- [ ] Each transport test uses a port that cannot collide with another test.
- [ ] The suite passes on repeated runs on Linux with the full test set.
- [ ] A failure reports which port or timeout was refused.
