# Loading screen

One subject: the full-screen loading layer, its visual rules, copy timing, and
data sources.

## Visuals

`LoadScreen` covers the screen while `GameState.Loading`. It draws:

- the base `ScreenBackground`;
- the `ui/menu/background` starfield material when it loads;
- a `UiTheme.Scrim` overlay;
- a centered 760 by 74 pixel content block.

The content block holds:

- the diegetic status line at 20 pixels in `UiTheme.Text`;
- a 1 pixel signal track with a 48 pixel cyan segment;
- the control hint at 16 pixels in `UiTheme.TextMuted`.

The signal segment moves back and forth across a 160 pixel track. It is an
indeterminate signal. Do not add a fake percentage.

The hint styles bracketed key names in the accent color. For example,
`[F5]` renders with a cyan key name.

The content fades in over 0.3 seconds. The background and scrim appear at once.

## Copy timing

The status and hint use separate timers.

- Status dwell: 2 seconds plus reading time at 150 words per minute.
- Hint dwell: 4 seconds plus reading time at 150 words per minute.
- The first hint starts 1 second after the status.
- A new status or hint does not repeat the previous value.

The timers reset each time the layer becomes visible.

## Data sources

`LocalizedTags` supplies the copy:

- `game_load` and `character_load` supply the status lines.
- `game_hints` supplies the control hints.

If a tag set is missing, the line is blank.

## Source

- Layer: `WaywardBeyond.Client/UI/Layers/LoadScreen.cs:18`
- Visibility and reset: `WaywardBeyond.Client/UI/Layers/LoadScreen.cs:51`
- Render: `WaywardBeyond.Client/UI/Layers/LoadScreen.cs:69`
- Copy timing: `WaywardBeyond.Client/UI/Layers/LoadScreen.cs:143`
- Signal line: `WaywardBeyond.Client/UI/Layers/LoadScreen.cs:165`
- Hint markup: `WaywardBeyond.Client/UI/Layers/LoadScreen.cs:235`
- Word count: `WaywardBeyond.Client/UI/Layers/LoadScreen.cs:269`
- Background material: `WaywardBeyond.Client/assets/materials/ui/menu/background.toml`

## Tests

No dedicated tests. The layer has no isolated clock or geometry helper.
