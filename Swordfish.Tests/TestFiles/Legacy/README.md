# Legacy UI goldens

These captures come from the last pre-migration UI commit, `d0e6c7cf`.

- `Pages/` holds page and overlay command snapshots from the old UI code at
  that commit.
- The root `*.txt` files hold component command snapshots from the old widgets.

`UiSnapshotTests.NormalizeVisual` compares the old and new captures. The
comparison ignores instance IDs and clip rectangles. It also treats an equal
fill color and background color as one fill because both paint the same pixels.

Keep these files unchanged. They are the reference for the old UI.
