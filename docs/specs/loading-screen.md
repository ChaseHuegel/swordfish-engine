# Loading screen

One subject: the full-screen loading layer, its visual rules, copy timing, and
data sources.

## Visuals

`LoadScreen` covers the screen while `GameState.Loading`. `LoadScreenView.rml`
draws a full-screen scrim and a centered 760 by 74 pixel content block.

The content block holds:

- the diegetic status line at 20 pixels;
- a 1 pixel signal track with a 48 pixel cyan segment;
- the control hint at 16 pixels.

The view fades each color from state alpha values supplied by the layer.

The signal segment scans left to right across a 160 pixel track. It exits the
right edge and re-enters from the left after a short offscreen gap. The full
cycle takes 2 seconds. It is an indeterminate signal. Do not add a fake
percentage.

The hint styles bracketed key names in the accent color. For example,
`[F5]` renders with a cyan key name.

The content fades in over 0.3 seconds. Each new status or hint also fades in
over 0.3 seconds. The overlay appears at once.

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
- View: `WaywardBeyond.Client/UI/Layers/LoadScreenView.rml:1`
- Visibility and reset: `WaywardBeyond.Client/UI/Layers/LoadScreen.cs:48`
- Render: `WaywardBeyond.Client/UI/Layers/LoadScreen.cs:68`
- Copy timing: `WaywardBeyond.Client/UI/Layers/LoadScreen.cs:96`
- Signal state: `WaywardBeyond.Client/UI/Layers/LoadScreen.cs:83`
- Hint formatting: `WaywardBeyond.Client/UI/Layers/LoadScreenView.cs:46`
- Word count: `WaywardBeyond.Client/UI/Layers/LoadScreen.cs:161`

## Tests

`Swordfish.Tests/OverlayViewTests.cs` snapshots the view at three window sizes.
