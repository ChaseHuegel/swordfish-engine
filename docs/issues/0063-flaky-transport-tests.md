# Bug: Transport tests flake when the suite runs in parallel

- Type: bug
- Status: open
- Workflow: ../specs/issues.md

## Problem

Several transport tests fail intermittently when the whole `Swordfish.Tests`
suite runs, and pass when run alone:

- `ServerRpcTests.TcpMenuCallsUseTheLanEndpointAndDenyRemoteWrites`
- `ServerWorldIntegrationTests.TcpJoinStreamSaveLeaveAndRejoin`
- `TransportPolicyTests.StalledPeerIsClosedWithBacklogLimit`

The TCP cases reach the 5 s pump deadline or fail with "connection closed"
after the idle timeout, which means the request never dispatched. All of them
bind or connect real sockets on the loopback interface. The failure rate grew as
the suite grew, which points to contention between parallel test collections
over ports or thread scheduling rather than a logic defect. The same three tests
fail at the same rate on the commit before the server review pass, so the flake
predates it.

A red build from an unrelated change is the cost. The gate cannot be trusted when
the suite is not stable.

## Acceptance criteria

- [ ] Each transport test uses a port that cannot collide with another test.
- [ ] The suite passes on repeated runs on Linux with the full test set.
- [ ] A failure reports which port or timeout was refused.
