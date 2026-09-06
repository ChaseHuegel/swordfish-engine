# Commit Guide

Compact reference for the commit style in this repo. Derived from the human-authored
commit history (short, one-line messages).

## Subject line

- Keep it to a single line. Do not write long, multi-paragraph bodies — those read as
  agent-authored and out of place here.
- Start with an imperative, present-tense verb, capitalized: `Fix`, `Add`, `Implement`,
  `Remove`, `Move`, `Update`, `Use`, `Make`, `Clean up`, `Hook up`.
- Write the rest in lowercase and keep it terse: "Add sqlite", "Fix inventory not
  saving", "store stats as key-values".
- No engineering prefixes (`feat:`/`fix:`/`chore:`), no scope parens, no emojis.
- No trailing period. Aim under ~60 characters when reasonable.
- Be concrete: name the specific thing changed rather than summarizing vagues.

## Issue references

- Append issue numbers at the end of the subject, separated by spaces.
- Examples: `Fix cursor deltas on Wayland #590`, `Multiple Reef fixes #592 #593 #594`.
- For a single issue, `Fix ... #NNN` (leading form like `#590 Fix ...` is acceptable but
  the trailing form is the norm).

## Examples from the history

- `Fix sluggish Linux Wayland window resizes #591`
- `Multiple Reef fixes #592 #593 #594 #595 #596`
- `Add partial to nsd gen`
- `Remove version key and simplify meta key`
- `Use KV for level saves`
- `store stats as key-values`