# Asset Art Style Guide

One subject: visual, geometric, and technical rules for authoring 3D item models,
block textures, materials, and icons in Wayward Beyond. See
[asset-definitions](asset-definitions.md) for TOML schemas and
[rendering-color](rendering-color.md) for color space rules.

## Visual identity

Wayward Beyond uses a retro-future industrial aesthetic (cassette-futurism).
Tools and machinery look like rugged spacewalk hardware built for gloved hands.

- Favor riveted casings, chamfered barrels, copper drive coils, exposed braided
  conduits, glowing capacitor tubes, air horns, and enclosed D-handles.
- Avoid sleek consumer or modern cordless tool shapes (such as slide-on bottom
  battery packs).
- Give every tool a distinct silhouette and primary accent color so it reads
  immediately in both hotbar icons and first-person view.
  - Mining laser (`laser`): gunmetal and steel body with orange, yellow, red,
    and lime accents.
  - Paint sprayer (`sprayer`): navy and cyan body with a white gravity canister,
    brass regulator, and magenta accents.
  - Impact drill (`drill`): hazard gold and copper body with a gunmetal socket
    nose, green capacitor cell, and crimson conduit.

## Color palette (Endesga-32)

Author all pixel-art textures exclusively from the Endesga-32 palette. The
in-repo palette reference lives at `WaywardBeyond.Bricks/assets/paints/colors.toml`.

| Role | Hex colors |
|---|---|
| Gunmetal and darks | `#181425`, `#262b44`, `#3a4466`, `#3e2731` |
| Steel and neutrals | `#5a6988`, `#8b9bb4`, `#c0cbdc`, `#ffffff` |
| Hazard yellows and oranges | `#fee761`, `#feae34`, `#f77622` |
| Copper, rust, and earth | `#ead4aa`, `#e4a672`, `#e8b796`, `#c28569`, `#d77643`, `#b86f50`, `#be4a2f`, `#733e39` |
| Reds and magentas | `#f6757a`, `#e43b44`, `#a22633`, `#ff0044`, `#b55088`, `#68386c` |
| Cyans and blues | `#2ce8f5`, `#0099db`, `#124e89` |
| Indicator greens | `#63c74d`, `#3e8948`, `#265c42`, `#193c3e` |

## 3D item and tool models

Canonical references live under `WaywardBeyond.Client/assets/meshes/items/`:
`laser.bbmodel` (`laser.obj`), `sprayer.bbmodel` (`sprayer.obj`), and
`drill.bbmodel` (`drill.obj`).

### Scale and coordinate system

- 16 Blockbench units equal `1.0` OBJ world unit (`0.0625` per unit).
- Forward points along `-Z`. Up points along `+Y`. Right points along `+X`.
- Center the main body on `X = 0` so the first-person `[Items.ViewModel]`
  transform (for example `Position = (0.5, -0.4, 0.1)` on `laser` or
  `Position = (0.5, -0.5, -0.3)` on `sprayer` and `drill`, with
  `Rotation = (0, 5, 0)`) aligns cleanly in first-person view.
- Keep overall tool extents near `X: [-0.35, 0.15]`, `Y: [-0.5, 0.5]`, and
  `Z: [-1.0, 0.25]`.

### Geometry construction

Match `laser.bbmodel` by combining few solid volumes with thin alpha-masked
planes. Do not stack chunky rectangular prisms for fine details.

- **Primary volumes:** Build the main barrel, housing, or canister from 1 to 3
  chamfered octagonal prisms (`type: "mesh"` in `.bbmodel`) or compact solid
  primitives.
- **Surface details:** Paint collars, bands, cooling louvers, panel seams, and
  rivets onto the UV texture instead of adding extra boxes.
- **Thin and detailed parts:** Use zero-thickness, double-sided alpha-masked
  planes (`type: "cube"` with one zero-size axis and only two opposing faces
  textured) for handles, pistol grips, D-handle guards, triggers, draped wiring
  or hoses, side shields, nozzle horns, and mounting brackets.
- **Slender dowels:** Use a thin `1x1` rotated bar (such as `laser` and `drill`
  side grips) only where a protruding grip needs 3D volume.
- **Texel density:** Maintain strict `1 px = 1 Blockbench unit` (`0.0625` world
  units) across all elements. Do not stretch small UV patches onto large faces.

### OBJ and Blockbench export rules

The engine parses `.obj` meshes in `Swordfish/IO/ObjParser.cs:22-124`.

- Always commit both `<name>.bbmodel` (Blockbench `4.10` `free` format with
  embedded base64 texture) and `<name>.obj` generated from the same geometry.
- Emit triangulated faces (`f v/vt/vn v/vt/vn v/vt/vn`) with 1-based indices.
  `ObjParser` reads exactly 3 vertices per `f` line.
- Wind every triangle counter-clockwise (CCW) when viewed from outside so face
  normals point outward and backface culling keeps exterior faces visible.
- Flip vertical UV coordinates from Blockbench top-left pixels to OBJ
  bottom-left space (`v_obj = 1 - py / texHeight`).

## Textures, materials, and icons

| Asset | Path | Format and rules |
|---|---|---|
| Item UV atlas | `WaywardBeyond.Client/assets/textures/items/<name>_uv.png` | `32x32` RGBA PNG, Endesga-32 colors, transparent pixels for masked cutouts |
| Item material | `WaywardBeyond.Client/assets/materials/items/<name>.toml` | `Shader = "lighted"`, `AlphaMode = "Masked"`, `Textures = ["items/<name>_uv.png"]` |
| Item icon | `WaywardBeyond.Client/assets/textures/items/<name>.png` | `96x96` transparent RGBA PNG, isometric front-left 3/4 view pointing up-left (match `laser.png`) |
| Block texture | `WaywardBeyond.Client/assets/textures/block/<name>.png` | `16x16` PNG, Endesga-32 colors (`<name>.0` through `<name>.15` for connected tiles) |
| Item definition | `WaywardBeyond.Items/assets/items/<name>.toml` | Set `Icon`, `[Items.ViewModel]`, and `[Items.WorldModel]` to the asset paths |
| Item copy | `WaywardBeyond.Client/assets/lang/en/item.toml` | Add display name and description localization keys |

Placeable brick items omit `Icon` so `BrickIconRenderer` renders their isometric
icon at startup.

## Agent authoring workflow

Follow this sequence when authoring a new 3D asset in a prompt:

1. Align on the silhouette, part breakdown, and Endesga-32 accent palette before
   generating files.
2. Generate `<name>.obj`, `<name>.bbmodel`, `<name>_uv.png`, and `<name>.png`
   from one deterministic script so meshes and UVs stay synchronized.
3. Render a 4-angle preview sheet (front 3/4, side profile, first-person, rear
   3/4) alongside `laser.obj` using nearest-neighbor sampling and backface
   culling.
4. Visually inspect the rendered PNGs to verify outward winding, connected
   silhouette pixels, UV orientation, and 1:1 texel density.
5. Show static embedded PNG previews for review before writing files into
   `WaywardBeyond.Client/assets/` and `WaywardBeyond.Items/assets/`.
6. Validate all `.obj` triangle indices and `.bbmodel` JSON structure, then run
   the targeted item test (`dotnet test WaywardBeyond.Tests --no-build --filter "SprayerToolTests"`).
