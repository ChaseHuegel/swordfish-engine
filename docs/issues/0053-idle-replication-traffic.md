# Bug: Idle worlds replicate physics state every snapshot

- Type: bug
- Status: done
- Workflow: ../specs/issues.md

## Problem

A world with no player input still sent a `WorldSnapshot` at `SnapshotHz` for
every networked body. The Jolt sync wrote `TransformComponent` and
`PhysicsComponent` through `Ref<T>.Write` on every fixed step. A write marks the
component dirty even when the value is unchanged. The sync also called
`SetPositionRotationAndVelocity` on every step for every body. That call resets
Jolt's sleep timer, so bodies never slept.

The client also uploaded an identical `InputComponent` at `SnapshotHz` while the
player was idle.

## Acceptance criteria

- [x] An unchanged body does not dirty `TransformComponent` or
      `PhysicsComponent`, and Jolt can put it to sleep.
- [x] An idle shared-simulation command does not dirty `PhysicsComponent`.
- [x] The client writes and uploads an input sample only when it changes, plus
      a 1 s keepalive.
- [x] Tests pin the idle and changed paths for physics sync, the simulation
      step, and client input.
- [x] The networking docs describe the change-gated behavior.
