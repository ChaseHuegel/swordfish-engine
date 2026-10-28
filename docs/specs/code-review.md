# Code review

The lens applied to a change before it is final. Use it for self-review and
peer review. The rules live in [development](../development.md) and
[specs/naming](naming.md). This doc says what to look for, what blocks a
change, and what becomes a follow-up. It does not restate the style tables.

## How to review

1. Read the change and its stated subject.
2. Check scope. A change is one subject.
3. Check correctness. Bugs, failure states, threading, replication.
4. Check structure. Placement, abstraction, dead code.
5. Check hot paths. Allocation and per-tick cost.
6. Check documentation. Contract, not implementation.
7. Run the docs pass.

Classify each finding:

| Class | Action |
|---|---|
| Blocking | Fix in this change. |
| Follow-up | Capture as an issue. Do not fix here. |
| Note | Mention it. Do not act. |

A problem outside the subject is a follow-up. Do not expand scope. See the
adjacent-problem rule in [specs/issues](issues.md).

## Scope

- One subject. The change is surgical.
- One module and its direct consumers.
- No engine and game in one commit.
- Builds and tests standalone.
- No drive-by refactors.

## Correctness

- `TODO`, `FIXME`, or `HACK`. Resolve it or capture it as an issue. Do not
  merge a new one.
- Bugs. Check bounds, nullability, defaults, units, and inverted conditions.
- Expected failure returns `Result` or `Result<T>`. Do not throw for a case the
  caller can handle.
- A new non-hot-path API does not use `bool TryGet(..., out T)`.
- A `Result<T>` caller checks `Success` before it reads `Value`. The implicit
  conversion to `T` yields `default` on failure.
- A contract change has its test updated or deleted in the same change.

## Structure

- A thin wrapper that only forwards a call. Inline it.
- An abstraction with one implementation and no churn pressure. Inline it.
- Parallel state. Keep one representation.
- Dead code, unused members, unreachable branches, commented-out code.
- A type in the wrong project. Depend downward only.
- A project that holds two unrelated concerns. Propose a split as a follow-up.
- A record or model that copies an nsd type field for field. Use the generated
  type.
- Names that break [specs/naming](naming.md) or a sibling module.

## Hot paths and threading

- Allocation per tick. Check closures, boxing, LINQ, `params` arrays,
  interpolation, iterator state machines, `ToArray`, and `ToList`.
- Unbounded work per entity or per message.
- Shared mutable state across threads without a store or queue.
- Store access from a foreign thread.
- A lock held across an external callback.
- An ECS system that assumes the render or input thread.

The client ECS ticks on the `"ECS"` thread. Server worlds tick on the
`"Server"` thread. See [architecture](../architecture.md).

## Networking and replication

- Change-gated writes. A write of an unchanged value marks dirty and defeats
  delta replication.
- One representation of networked state.
- Bounded or pooled snapshot and input buffers.
- Hostile or malformed peer input validated before use. Check length, count,
  and id.
- Correlation and error paths preserve state.

See [networking-replication](networking-replication.md),
[networking-join](networking-join.md), and
[networking-messages](networking-messages.md).

## Documentation

- A public, internal, or non-obvious type, member, or constructor lacks a
  one-line summary.
- An override, an interface implementation, or a refined member lacks
  `<inheritdoc/>`.
- A type summary restates its members.
- A summary explains the implementation, a decision, or a plan.
- A code comment references `docs/`, an issue, or a plan.
- A comment describes code that reads in one line.
- The docs pass is done and the `file:line` pointers are current.

## Smells

- A method with a boolean flag that selects between two behaviors.
- A method body nested past five indentation levels, or a multi-line argument
  inside a condition. See the nesting rules in [development](../development.md).
- A `Manager`, `Helper`, or `Util` type that owns unrelated work.
- A getter with side effects.
- A catch that swallows without logging.
- A `default!` that hides a real failure.
- A cast chain that encodes a type switch.
- A public setter that bypasses the store or validation.
- A comment that explains a workaround instead of removing it.

## Do not flag

- `TryGetValue` on a dictionary-shaped type.
- ECS `TryGet` and other per-tick lookups.
- A concrete type where an abstraction is not earned.
- A short summary. Brevity is the goal.
- A style-table rule. Link [development](../development.md) instead of
  repeating it.

## Gate

A change is ready when:

- The build is clean.
- The tests pass, or the change updates or deletes the tests it invalidates.
- No blocking finding remains.
- Follow-ups are captured as issues.
- The docs pass is done.
