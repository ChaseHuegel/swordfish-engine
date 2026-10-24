# Bug: Docs reference a missing client test project

- Type: bug
- Status: done
- Workflow: ../specs/issues.md

## Problem

Agent-facing docs tell readers to run `dotnet test WaywardBeyond.Client.Tests`,
but the project no longer exists in the repository or the solution. The
command fails with `MSB1009: Project file does not exist`.

The stale references are in `AGENTS.md`, `docs/development.md`, and the
"Tests that pin this" sections of `docs/specs/persistence.md`,
`docs/specs/chat.md`, `docs/specs/networking-voxel-edits.md`,
`docs/specs/brick-identity.md`, `docs/specs/networking-messages.md`, and
`docs/specs/networking-inventory.md`. The cited test files (for example
`PlayClockTests.cs`, `LevelsClientTests.cs`) are gone with the project.

The game test suite is unmaintained by policy. Either restore the test
project or remove every reference to it.

## Acceptance criteria

- [x] `dotnet test WaywardBeyond.Client.Tests` no longer appears in
      `AGENTS.md` or `docs/development.md`, or the project is restored to the
      solution.
- [x] No doc cites a `WaywardBeyond.Client.Tests` test file that does not
      exist.