# Improvement: Remove the feedback submit delay

- Type: improvement
- Status: open
- Workflow: ../specs/issues.md

## Problem

`FeedbackModal` submits on a background task after `Task.Delay(100)`. The delay
exists only so the modal has closed before the log snapshot is taken. A slow
frame makes the delay too short, and every submit pays it even when nothing
depends on it.

## Acceptance criteria

- [ ] The log snapshot is taken without a fixed delay.
- [ ] The modal closes before the submission runs, or the submission does not
      depend on the modal state at all.
- [ ] A failed submission still reports to the player.
