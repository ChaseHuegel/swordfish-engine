# Issue Index and Workflow

One subject: how issues are recorded, indexed, and worked in this repository.
It defines the issue document format, the active-sprint workflow, and the
commands for addressing issues. Working norms live in
[development](../development.md) and [AGENTS.md](../../AGENTS.md).

## Issue documents

Each issue is a loose markdown document under `docs/issues/`. One issue per
document. Name the file `0000-short-slug.md` with a zero-padded, ascending
number.

Every issue document uses this template:

```markdown
# <Type>: <Title>

- Type: bug | improvement | research
- Status: open | in-progress | done
- Workflow: ../specs/issues.md

## Problem

Describe the current behavior and why it is wrong. For research, state the
question to answer.

## Acceptance criteria

- [ ] Describe a verifiable outcome.
- [ ] Describe the second verifiable outcome.
```

The type is one of `bug`, `improvement`, or `research`. The status is one of
`open`, `in-progress`, or `done`.

## The index

The live index is `docs/issues/index.md`. It has two sections, Active Sprint
and Backlog. Each section holds three type subsections: Bug, Improvement, and
Research.

List an issue in the index as a checkbox bullet that links to its document:

```markdown
- [ ] [Title](/docs/issues/0000-slug.md) — one-line summary
```

A pending issue is unchecked (`- [ ]`). A done issue is checked (`- [x]`). A
done issue stays in its subsection, checked in place.

Sprint rotation and backlog ordering are user-managed. Do not archive, roll, or
rotate sprints.

## Adding an issue

1. Create the issue document in `docs/issues/`.
2. Add an unchecked `- [ ]` entry under the matching section and type
   subsection in `docs/issues/index.md`.

## Working an issue

An agent works one issue at a time through this lifecycle:

1. Read and understand the issue.
2. Research and explore the issue when it is complex or vague.
3. Form a development plan.
4. Self-review the plan before implementation.
5. Implement the change.
6. Self-review the implemented change.
7. Mark the issue complete.
8. Commit the change.

Follow the standards in [development](../development.md) and the quality gates
in [AGENTS.md](../../AGENTS.md). Run the docs pass before finalizing.

### Adjacent problems

A problem found during development that is not directly relevant to the issue
being worked is an adjacent problem. Do not fix it. Report it to the user and
capture it as a new issue in the Backlog. Fix only what the issue requires.

## Marking an issue complete

1. Set the issue document status to `done`.
2. Check the box (`- [x]`) for that issue in `docs/issues/index.md`.
3. Keep the issue in its subsection.

## Commands

- Work the active sprint. Find every unchecked item in Active Sprint and
  address it sequentially, in order, until nothing remains.
- Work the next issue. Find the next unchecked item in Active Sprint.
- Work the next bug. Find the next unchecked item of the named type in Active
  Sprint. The named type is one of `bug`, `improvement`, or `research`. When no
  unchecked item of that type exists, report that none remains.