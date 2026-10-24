# Bug: CLI parser splits quoted multi-word values

- Type: bug
- Status: open
- Workflow: ../specs/issues.md

## Problem

The CLI parser splits a quoted multi-word option value into several values.
`--name "Smoke Test"` parses as `['name'=['Smoke', 'Test']]`. Single-value
consumers take the first element and silently drop the rest. The server
launcher persists `Name = "Smoke"` for `--name "Smoke Test"`.

The parse rule lives in `Shoal/CommandLine/`. The token matchers are in
`CommandLineParser.cs:7-14`. The value loop is in `CommandLineTokenParser.cs`
(`TryReadArg`, `TryReadQuoteOrText`). The truncation is in
`CommandLineArgs.cs:47-57` (`TryGetValue` reads `values[0]`).

The dedicated server's `--name` is the known consumer
(`WaywardBeyond.Server.Launcher/Program.cs`, `ApplyCliOverrides`). Any future
option that carries a space hits the same defect.

## Acceptance criteria

- [ ] `--name "Smoke Test"` parses to one value `Smoke Test` with quotes
      stripped and the space kept.
- [ ] `CommandLineArgs.TryGetValue` returns the full quoted value, pinned by a
      `Swordfish.Tests` case on the parser.