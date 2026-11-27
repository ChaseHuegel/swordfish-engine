# Asset Tooling

One subject: the desktop tool and CLI for editing Wayward Beyond content TOML
and blueprint files. It records the stack, the project boundaries, the write
contract, the validation rules, and the completed delivery phases.

Status: implemented. The tool, CLI, shared meshing library, and packaging
scripts are as-built. Each fact links to its source.

## Scope

The tool edits this content:

- Items, bricks, paints, recipes, skills, and bodies. See [asset-definitions](asset-definitions.md).
- Materials, which are engine-side flat TOML.
- Localization tables and invariant tag lists.
- Blueprint `.wbp` files. See [region-blueprints](region-blueprints.md).
- Content packs, meaning `manifest.toml`, pack metadata, and the `assets/` tree.

The tool validates content with the same loaders the game uses. It never loads
module assemblies. Manifests and metadata are data.

The tool does not author voxels, textures, meshes, or shaders. It does not edit
particle TOML or CSV name tag files. It does not run git operations. It does not
run mod code.

## Audience and platforms

| Audience | Platform |
|---|---|
| Modders | Windows 10 x64 |
| Developers | Linux x64, glibc 2.35 or newer |

There is no macOS build and no arm64 build. The tool UI is localized from the
first release. Only English ships today.

## Stack

| Layer | Decision |
|---|---|
| Runtime | .NET 9 |
| UI | Avalonia 11 and CommunityToolkit.Mvvm |
| Workspace | One workspace per app instance |
| Localization | `.resx` catalogs and a typed accessor |
| GL | Avalonia `OpenGlControlBase` and Silk.NET.OpenGL |
| GL paths | Windows WGL, then ANGLE. Linux GLX, then EGL |
| Shader | One unlit body with `330 core` and `300 es` preambles |
| Read, authoritative | Tomlet with the shipped options |
| Read, strict | Tomlet without the forgiving options |
| Document | Tomlyn syntax tree and span edits |
| Search | FuzzySharp for distance. The index owns ranking |
| Imaging | Avalonia imaging and `Swordfish.Util.Imaging` |
| Preview | Unlit albedo with baked vertex light |
| Tests | xunit, Avalonia.Headless, Xvfb with llvmpipe |
| Packaging | Self-contained folder per OS |

## Projects

```
WaywardBeyond.Meshing          Shared CPU voxel meshing and PreviewMesh
WaywardBeyond.AssetTools       Workspace, validation, edits, forms, and index
WaywardBeyond.AssetTools.Cli   validate | format | roundtrip | new | ids
WaywardBeyond.AssetStudio      Avalonia shell, SceneRenderer, and GlViewport
```

Dependency rules:

- `AssetTools` may reference `Swordfish` for engine data types and utilities
  (`MaterialDefinition`, `ObjParser`, `Swordfish.Util.Imaging`) and `Shoal`
  transitively for crash handling, logging, and module manifests. It never
  references `WaywardBeyond.Client` and never starts the engine runtime.
- `AssetStudio` references `AssetTools`.
- `Meshing` references `Voxels`, `Bricks`, `Data`, and `Swordfish.Library`. It
  never references `Swordfish.Graphics`.
- `Tools.slnf` holds the tool projects and tool test suites. `Tests.slnf`,
  `Engine.slnf`, and `Game.slnf` do not change.

## Production prerequisites

All five prerequisites are complete:

1. `MaterialDefinition` (`Swordfish/IO/MaterialDefinition.cs:6`) and `ObjParser`
   (`Swordfish/IO/ObjParser.cs:8`) are public.
2. All game definition types are public (`WaywardBeyond.Bricks/BrickDefinition.cs:6`).
3. `WaywardBeyond.Meshing` holds `MeshPostPass`, `MeshState`,
   `VoxelSurfaceCache`, `CubeMeshBuilder`, `ConnectedTextureResolver`,
   `BrickPreviewMeshBuilder`, and `RegionMeshBuilder`. It outputs `PreviewMesh`
   (`WaywardBeyond.Meshing/PreviewMesh.cs:6`) and looks up textures through
   `ITextureLayerLookup` (`WaywardBeyond.Meshing/ITextureLayerLookup.cs:4`).
   The client keeps four adapters.
4. `ContentVersion.CURRENT` lives in `WaywardBeyond.Data/ContentVersion.cs:7`.
   `WaywardBeyond.Tests/ContentVersionTests.cs` fingerprints the field surface
   of every definition type and fails until the constant bumps.
5. Module loading behavior did not change.

## Workspace model

The tool has two workspace modes.

| Mode | Writable roots | Reference roots |
|---|---|---|
| Repo | Module directories in the checkout | None |
| Pack | One pack directory | A game install |

Roots are ordered. Order comes from the load order that the game uses. Every
file records its provider. A file that another root shadows shows both
providers. The asset tree shows provenance. `AssetTools` shares the game
`*Definition` structs and `Toml` parser, and owns its own multi-root provenance
index and validation rules rather than instantiating the game `*Database`
classes.

Overrides are allowed. The database overwrites an asset with the same ID by
load order (`Swordfish.Library/Collections/VirtualAssetDatabase.cs:75`), and a
later mount replaces a file at the same virtual path
(`Swordfish.Library/IO/VirtualFileSystem.cs:101`). The tool badges overrides.
It does not treat them as fatal.

The tool reads `config/modules.toml` to show whether a pack is enabled. It does
not write that file. At runtime, `Shoal` discovers manifests across `modules/`,
`<DataRoot>/modules`, and configured additional roots, and merges
`<DataRoot>/config/modules.toml` with the install `config/modules.toml`.
Wayward Beyond registers `<DataRoot>/packs` as a content-only root through
`PackModuleResolution.CreateOptions`
(`WaywardBeyond.Config/PackModuleResolution.cs:19`). See
[config-schemas](config-schemas.md).

### Opening a workspace

The desktop window opens maximized (`WaywardBeyond.AssetStudio/App.axaml.cs:46`).
The start screen offers recent workspaces, open repo, open pack, open install,
and new pack. Both the start screen and Settings allow configuring a
per-workspace default namespace. Each recent workspace row includes a delete
button (`WaywardBeyond.AssetStudio/Views/StartScreenView.axaml:165`) that opens
a confirmation modal
(`WaywardBeyond.AssetStudio/ViewModels/DeleteRecentWorkspaceDialogViewModel.cs:10`,
`WaywardBeyond.AssetStudio/Views/Dialogs/DeleteRecentWorkspaceDialog.axaml:1`).
By default, confirming removes the entry from `RecentWorkspaces` in
`ToolSettings` (`WaywardBeyond.AssetTools/Settings/ToolSettings.cs:153`) while
keeping files on disk intact. Checking the opt-in disk deletion toggle deletes
the workspace folder recursively when it does not target a protected root,
home, or system directory
(`WaywardBeyond.AssetStudio/Services/AssetToolsStudioWorkspaceService.cs:161`).

The install locator checks the Windows registry, the Steam roots on Linux, and
Proton prefixes under `compatdata`. It always allows a manual browse. It never
guesses when more than one install is found.

When a workspace contains no assets, `EditorHostView`
(`WaywardBeyond.AssetStudio/Views/EditorHostView.axaml.cs:8`),
`WorkspaceExplorerView`
(`WaywardBeyond.AssetStudio/Views/WorkspaceExplorerView.axaml.cs:13`), and
`InspectorPaneView`
(`WaywardBeyond.AssetStudio/Views/InspectorPaneView.axaml.cs:10`) show calm
empty states instead of active-document forms. Valid empty packs offer a
`+ Create First Asset` action. Uninitialized folders that lack `manifest.toml`,
`pack.toml`, and `assets/` (`AssetSeedProjector.HasPackStructure` at
`WaywardBeyond.AssetStudio/Services/AssetSeedProjector.cs:68`) show an amber
`Folder:` badge and a one-click `Initialize Content Pack Here` action.

### Settings

Settings are per user (`WaywardBeyond.AssetTools/Settings/ToolSettings.cs:6`).
They hold recents, panel layout, culture override, theme, UI scale,
per-workspace default namespaces (`WorkspaceDefaultNamespaces`), GL preference,
and log level. Theme defaults to `ToolTheme.System`, and a three-icon switcher
toggles System, Light, and Dark modes. `StudioTheme.axaml`
(`WaywardBeyond.AssetStudio/Themes/StudioTheme.axaml:100`) and
`StudioStyles.axaml` (`WaywardBeyond.AssetStudio/Themes/StudioStyles.axaml:863`)
define matching Dark and Light theme styles and flyout brushes for
`ContextMenu`, `MenuFlyoutPresenter`, and `MenuItem` rows including danger
actions. `UiScale` clamps to `0.75` through `1.75` and adjusts via `Ctrl+`,
`Ctrl-`, `Ctrl+0`, or the status bar slider. A `portable` marker file next to
the executable keeps settings beside it instead.

## Document model

Text is the document of record.

- The Tomlyn syntax tree holds comments, order, and whitespace.
- Each field is a span. The span holds raw text and an optional typed value.
- An invalid value stays in the document. Saving writes it back unchanged.
- The tool never reformats a file on save. `format` is a separate command.
- Alignment is re-applied only inside a table the user edited, and only when
  that table was aligned before.

Two parse paths exist.

| Path | Purpose |
|---|---|
| Authoritative | Reproduce what the game loads |
| Strict | Report what the forgiving options hide |

The shipped options set `IgnoreInvalidEnumValues = true`
(`Swordfish.Library/Serialization/Toml/Toml.cs:13`). Strict mode turns this off.
A field that fails strict parsing shows a danger style and a raw text editor. A
dropdown is an explicit replacement action.

A file with a syntax error opens as raw text. It still saves. The form is
disabled.

## Drafts and saving

- A draft is a full text copy of one file.
- Drafts live in the user data directory, keyed by destination path.
- A draft stores the base file hash. The tool uses the hash to detect external
  changes.
- A draft is written on input settle. A crash keeps the draft.
- Startup offers recovery for drafts from an unclean exit.
- App-wide undo and redo run through `StudioUndoStack`
  (`WaywardBeyond.AssetStudio/Services/StudioUndoStack.cs:6`) via `Ctrl+Z`,
  `Ctrl+Y`, and header buttons. Form `TextBox` controls set
  `IsUndoEnabled="False"` so text edits participate in the app stack.
- The Save `SplitButton` saves the active document or discards its unsaved
  changes and prunes its undo history.
- `WorkspaceFileWatcher`
  (`WaywardBeyond.AssetStudio/Services/WorkspaceFileWatcher.cs:8`) watches
  active roots and reloads unmodified files on external disk changes while
  preserving dirty drafts.
- `AssetDeleter` (`WaywardBeyond.AssetTools/Editing/AssetDeleter.cs:11`) deletes
  standalone files, single entries in multi-entry TOML files, or single keys in
  `[Tags]` files from the explorer context menu or `Delete` key after
  confirmation. It refuses to delete files outside writable workspace roots.
- `AssetCloner` (`WaywardBeyond.AssetTools/Editing/AssetCloner.cs:15`) and
  `AssetIdAllocator` (`WaywardBeyond.AssetTools/Editing/AssetIdAllocator.cs:10`)
  power `Copy`, `Paste`, and `Duplicate` actions in the workspace explorer
  `ContextMenu` (`WaywardBeyond.AssetStudio/Views/WorkspaceExplorerView.axaml:39`).
  Pasting and duplicating allocate conflict-free `_2` and `_3` suffixes across
  all roots, preserve comments and subtables in multi-entry TOML files, write
  sibling `.toml` or `.wbp` files for materials and blueprints, and reject
  read-only targets (`MainWindowViewModel.PasteAsset` at
  `WaywardBeyond.AssetStudio/ViewModels/MainWindowViewModel.cs:1922`).
- Save writes a temp file in the destination directory. It then fsyncs,
  renames, and verifies.
- A failed multi-file save rolls back in reverse order. The tool writes a
  recovery manifest.
- Edits never auto-merge with an external change.
- Status notifications appear as toasts that auto-dismiss after 5 seconds.

## Forms and pickers

Every form has a fixed identity header with the identity fields that the kind
defines:

- ID. Read-only after creation. Rename is an explicit action. Rename previews
  affected files and references, then applies edits to in-memory drafts as one
  undoable workspace operation saved via Save All.
- Display name key and description key.
- Icon.
- Namespace badge.

Multi-entry TOML files are supported (such as `colors.toml` and `textures.toml`
for paints). Creating a new asset pre-fills the workspace default namespace,
derives the target filename from the asset ID, and prompts the user to choose a
new file or an existing file in the kind folder. `AssetScaffolder`
(`WaywardBeyond.AssetTools/Editing/AssetScaffolder.cs:10`) appends `.toml`
automatically for TOML asset kinds when the target path omits an extension.

Body sections are collapsible. Their order follows authoring frequency. Collapse
state is remembered per kind.

| Kind | Body sections |
|---|---|
| Item | Placeable, tool, statistics (`[Items.Statistics]` with inline key/value validation and save guard), view model, world model, stack |
| Brick | Shape, color, alpha, cast shadows, passable, statistics (`[Bricks.Statistics]` with inline key/value validation and save guard), mesh, per-face textures, sounds, colliders |
| Paint | Kind switch, alpha, connected, variants or color |
| Recipe | Target and execution fields (with searchable tag dropdown), editable `[[Recipes.Inputs]]` and `[[Recipes.Outputs]]` tables with searchable item ID and `tag:*` dropdowns, jump buttons, Command Palette search tokens, and live inbound-reference sync on save (`RecipeFormView`, `AssetTomlEditApplier.ApplyRecipeEdits`) |
| Skill | Sources map, level curve table and sparkline |
| Body | Species, gender, editable state and direction sprite maps (`AssetTomlEditApplier` and `BodySpritePickerControl`) |
| Material | Shader, textures, alpha mode, cutoff |
| Blueprint | Name, size, brick count, timestamps, clickable Brick and Paint Palette |

`SkillFormView` (`WaywardBeyond.AssetStudio/Views/Forms/SkillFormView.axaml:16`)
exposes `Copy Curve` and `Paste Curve` buttons in the Level Curve header.
`MainWindowViewModel` (`WaywardBeyond.AssetStudio/ViewModels/MainWindowViewModel.cs:1848`,
`:1870`) copies curves across skills, `AssetDocumentViewModel.ReplaceSkillLevels`
(`WaywardBeyond.AssetStudio/ViewModels/AssetDocumentViewModel.cs:2041`) applies
pasted levels as one undo entry, and `AssetTomlEditApplier.ApplySkillLevelEdits`
(`WaywardBeyond.AssetStudio/Services/AssetTomlEditApplier.cs:405`) saves the
curve to `[Skills.Levels]`.

`WorldModel`, skill `Icon`, and skill `Category` are marked informational because
no code reads them today.

Every scalar field has a raw-token toggle. The toggle shows the exact text in the
file.

Standard reference fields use a searchable combo (`ReferencePickerControl` at
`WaywardBeyond.AssetStudio/Controls/ReferencePickerControl.axaml.cs:8`). Each
row shows kind, ID, provenance, and resolution state. Material candidates render
inline 22x22 thumbnails backed by `ThumbnailCache`
(`WaywardBeyond.AssetStudio/Services/ThumbnailCache.cs:10`).

All texture fields share a searchable visual thumbnail grid and an optional
native disk file picker button (`IWorkspacePathPicker.PickImageFileAsync` at
`WaywardBeyond.AssetStudio/Services/IWorkspacePathPicker.cs:21`). This covers
Body sprites (`BodySpritePickerControl` at
`WaywardBeyond.AssetStudio/Controls/BodySpritePickerControl.axaml.cs:15`), Item
`Icon`, Skill `Icon`, Material `Textures`, Brick face layers (`TextureDefault`
through `TextureRight`), and Paint `Texture Variants` (`ReferencePickerControl`).
`AssetSeedProjector.ProjectWorkspaceImages`
(`WaywardBeyond.AssetStudio/Services/AssetSeedProjector.cs:173`) scans enabled
workspace roots recursively, excludes PBR channel sidecars
(`AssetSeedProjector.IsPbrChannelSidecar` at
`WaywardBeyond.AssetStudio/Services/AssetSeedProjector.cs:47`), and sorts
`characters/` entries first for body sprite picking. Tiles display filenames
with ellipsis truncation. The disk picker starts at the workspace root or the
last opened directory, and normalizes chosen paths back to texture-relative
paths (`IStudioWorkspaceService.ResolveImagePathForWorkspace` at
`WaywardBeyond.AssetStudio/Services/IStudioWorkspaceService.cs:231`) or bare
block layer names (`IStudioWorkspaceService.ResolveBlockLayerNameForWorkspace`
at `WaywardBeyond.AssetStudio/Services/IStudioWorkspaceService.cs:236`).

Mesh fields (`Brick.Mesh`, `Item.ViewModel.Mesh`, and `Item.WorldModel.Mesh`)
also use the searchable visual thumbnail grid
(`WaywardBeyond.AssetStudio/Controls/ReferencePickerControl.axaml:89` and
`StudioFieldViewModel.IsMeshPickerField` at
`WaywardBeyond.AssetStudio/ViewModels/StudioFieldViewModel.cs:290`) and a native
`.obj` disk file picker (`IWorkspacePathPicker.PickMeshFileAsync` at
`WaywardBeyond.AssetStudio/Services/IWorkspacePathPicker.cs:26`).
`MeshThumbnailRenderer.TryRenderThumbnail`
(`WaywardBeyond.AssetStudio/Services/MeshThumbnailRenderer.cs:25`) renders
isometric clay previews on the CPU without OpenGL, `ThumbnailCache`
(`WaywardBeyond.AssetStudio/Services/ThumbnailCache.cs:10`) caches the bitmaps,
and `IStudioWorkspaceService.ResolveMeshPathForWorkspace`
(`WaywardBeyond.AssetStudio/Services/IStudioWorkspaceService.cs:241`) normalizes
chosen paths relative to `assets/meshes/`.

Enum dropdowns offer valid game enum values, and shader pickers list real
workspace shaders. Clickable color swatches open `StudioColorPickerControl`
(`WaywardBeyond.AssetStudio/Controls/StudioColorPickerControl.axaml.cs:15`). An
invalid current value stays visible as a danger row. It is never cleared.

"Create new" exists only for localization keys. It prompts for the target
localization file (defaulting to the kind file, such as `lang/en/item.toml`) and
initial text. Bricks, materials, textures, and meshes cannot be created from a
picker.

Localization tables open in `LocalizationEditorView`
(`WaywardBeyond.AssetStudio/Views/LocalizationEditorView.axaml.cs:10`) as a
per-file key-value table with a locale selector. Translation keys edit inline
with duplicate-key highlighting, per-row delete, a guaranteed-unique default key
generator, a bottom inline `+ Add Key` row in addition to the top button, and
full Undo and Redo support. `AssetTomlEditApplier.ApplyLocalizationEdits`
(`WaywardBeyond.AssetStudio/Services/AssetTomlEditApplier.cs:741`) persists key
renames in place through `TomlDocument.RenameKey`
(`WaywardBeyond.AssetTools/Documents/TomlDocument.cs:731`) and removes deleted
entries through `TomlDocument.RemoveField`
(`WaywardBeyond.AssetTools/Documents/TomlDocument.cs:439`) without disturbing
comments or untouched lines.

Each reference kind has its own grammar, and the field hint states it:

| Field | Grammar | Example |
|---|---|---|
| Texture | Path with extension | `items/drill.png` |
| Material | Path without extension | `bricks/light` |
| Mesh | Path with extension | `items/drill.obj` |
| Brick face | Bare layer name | `core` |
| Sound tag | Invariant tag name (`lang/tags/sounds.toml`) | `metal_place` |
| Audio | Path with extension (inside `sounds.toml`) | `sounds/place/metal/metal_place.1.wav` |
| Blueprint | Relative path without `.wbp` (`assets/blueprints/`) | `builtin/core` |
| Localization | Dot key | `item.base.core` |

On Item forms, `Placeable.Type` accepts `none`, `brick`, or `blueprint`. Changing
the type dynamically reconfigures `Placeable.ID` between `ReferenceKind.BrickId`
and `ReferenceKind.BlueprintId`
(`WaywardBeyond.AssetStudio/Models/ReferenceKind.cs:6`,
`WaywardBeyond.AssetStudio/ViewModels/AssetDocumentViewModel.cs:2615`).
`AssetTomlEditApplier.ApplyItemPlaceableEdits`
(`WaywardBeyond.AssetStudio/Services/AssetTomlEditApplier.cs:122`) inserts,
updates, or removes `[Items.Placeable]` losslessly.

## Tag editor

The tag editor has two panes.

- File order is editable.
- Effective order is read-only. It shows the merged result and the provider of
  each value.

Insert appends by default. Dragging a row by its grip handle reorders values
(`WaywardBeyond.AssetStudio/Views/TagEditorView.axaml.cs:15`), and the first
reorder warns once. Delete removes a value. Duplicates are flagged and are not
merged. Each operation is one undo entry. The effective pane updates live.

In pack mode, a tag list from the reference root is read-only. In repo mode, the
editor writes the file in the module that owns it.

## Validation

Four severities:

| Severity | Meaning | Examples |
|---|---|---|
| Fatal | The content cannot load | TOML syntax error, missing ID, duplicate ID |
| Broken | The game reads this and it fails | Missing texture, dangling tag value, missing localization key |
| Cross-platform | It depends on the platform | Case-only path difference, CRLF or BOM drift |
| Style | It is legal and risky | Tag order change, ID rename |

Severity is consumer-aware. A reference in a field the game reads is broken. A
field with no reader is informational. `WorldModel`, skill `Icon`, and skill
`Category` are such fields today.

Rules that exist because the runtime is forgiving:

- Strict enums.
- Duplicate IDs across roots, with override badging.
- Case-exact paths, with a fix-case action.
- Tag order warnings. Order is semantic
  ([asset-definitions](asset-definitions.md)).
- Brick ID rename warnings. Legacy saves reverse-map by hash.
- Texture layer misses. The game falls back to layer 0
  (`WaywardBeyond.Meshing/VoxelSurfaceCache.cs:390`).
- Wrong-grammar references. Texture ids keep the extension. Material ids drop
  it. Brick face entries are bare layer names.
- Missing and duplicate localization keys.
- Item `PlaceableType.Blueprint` IDs against `WorkspaceIndex.WinningBlueprintFiles`
  (`WaywardBeyond.AssetTools/Workspace/WorkspaceIndex.cs:9` and
  `WaywardBeyond.AssetTools/Validation/WorkspaceValidator.cs:613`), emitting
  `PATH001` with a safe bulk fix for case drift and `REF001` for missing
  blueprints. Inbound references from blueprint placeable items also appear on
  blueprint documents.

The tool does not report unused localization keys. Keys can come from C# code.

### Quick fixes

`WorkspaceValidator` (`WaywardBeyond.AssetTools/Validation/WorkspaceValidator.cs:16`)
attaches `ValidationFixKind` actions (`WaywardBeyond.AssetTools/Validation/ValidationFixKind.cs:6`)
to deterministic findings. `ValidationBulkActions.ApplySingleFix`
(`WaywardBeyond.AssetTools/Validation/ValidationBulkActions.cs:122`) runs one
fix atomically, and `ValidationBulkActions.FixAllSafeIssues`
(`WaywardBeyond.AssetTools/Validation/ValidationBulkActions.cs:174`) batches
non-destructive safe fixes per file in descending span order while skipping
read-only roots.

| Scope | Codes | Behavior |
|---|---|---|
| Safe bulk (`IsSafeBulkFix = true`) | `FMT001`, `FMT002`, `PATH001`, `REF002`, `TAG001`, `ENUM001`, `COLOR001` | Strip UTF-8 BOM, convert CRLF to LF, fix exact path/ID/tag/key casing, normalize resource or localization grammar, remove duplicate tag array items, normalize exact enum casing or separators, and normalize 3-digit or 6-digit hex RGB |
| Single-item only (`IsSafeBulkFix = false`) | `COLOR001`, `ENUM001`, `REF001`, `LOC001`, `ID002`, `ID003`, `SKILL001`, `SKILL002`, `PAINT001` | Strip alpha from 8-digit hex, suggest unique closest enum or reference match, create missing English localization key, allocate next unique ID, insert `[Skills.Levels]` `1 = 100`, clamp XP below `1` to `1`, or remove conflicting `Color` on texture paint |
| Manual only (`ValidationFixKind.None`) | `TOML001`, `TOML002`, `ID001`, `COLLIDER001`, `TEX002`, `LOC002`, `SKILL003`, `STAT001`, `STYLE001`, `STYLE002` | Syntax errors, deserialize errors, missing IDs, invalid colliders, incomplete connected sequences, duplicate localization keys, zero XP sources, invalid brick or item statistics, style warnings, and unresolvable `ENUM001`, `REF001`, `PAINT001`, or `SKILL002` findings |

Live scalar fixes update open writable form fields with undo support, and
structural or disk fixes apply directly to disk and reload non-dirty documents
(`MainWindowViewModel.ApplyValidationQuickFix` at
`WaywardBeyond.AssetStudio/ViewModels/MainWindowViewModel.cs:2042`).

### Panel

The panel groups by severity, then root, then file. File groups start collapsed.
Clickable severity count badges filter the list by severity.

A row shows severity, message, file and line, and field. A click moves to the
span and focuses the field. Each fixable row exposes an inline `Fix` button
(`WaywardBeyond.AssetStudio/Views/ValidationDockView.axaml:208`), and
right-clicking a row opens a `Fix >` context menu for deterministic fixes and
missing localization key creation.

Bulk actions in the panel header are `Fix All Safe`
(`WaywardBeyond.AssetStudio/Views/ValidationDockView.axaml:49` and
`MainWindowViewModel.FixAllSafeIssues` at
`WaywardBeyond.AssetStudio/ViewModels/MainWindowViewModel.cs:2005`), fix-case,
format-file, and revalidate. Copy-all writes plain text.

The status bar shows counts. Only fatal and broken findings block export.

## CLI

The CLI has five commands:

| Command | Purpose |
|---|---|
| `validate <root>` | Report findings |
| `roundtrip <root> [--check]` | Check byte-identical no-op writes |
| `format <root>` | Apply house formatting |
| `new pack <dir>` | Scaffold a pack |
| `ids <root>` | List asset IDs by kind |

Results go to stdout. Logs go to stderr. `--json` writes a stable document with a
`schemaVersion` field. There are no prompts. Color is disabled when stdout is not
a terminal.

Exit codes are 0 for clean, 1 for findings at broken or worse, and 2 for a usage
or IO failure.

## Search and navigation

One command palette searches the workspace and opens with `F1`.

- Assets by ID, display name, and localization key.
- Resources by path and file stem.
- Actions, such as new item or validate workspace.

Ranking runs exact, then prefix, then token, then fuzzy. Filters select kind and
scope. Results show provenance and resolution state. Enter opens the winning
file. A modifier opens the shadowed source.

Documents carry a composite `DocumentKey` to disambiguate duplicate IDs across
roots or files. Selecting an asset expands its collapsed explorer group and
brings the row into view (`BringIntoView`). The right panel lists inbound
references for the current asset. A navigation stack holds back and forward
history.

## Preview

`SceneRenderer` owns every GL call and drains stale driver errors before checks.
It can draw into a framebuffer. `GlViewport`
(`WaywardBeyond.AssetStudio/Rendering/GlViewport.cs:21`) and
`PreviewViewportControl`
(`WaywardBeyond.AssetStudio/Controls/PreviewViewportControl.cs:20`) implement
`ICustomHitTest` and handle context, resize, DPI, orbit, pan, wheel zoom
(including while actively dragging an orbit), and invalidation. Orbit
auto-rotation is enabled by default in 3D previews
(`WaywardBeyond.AssetStudio/ViewModels/MainWindowViewModel.cs:103`) and pauses
during manual drag or pan. Brick and item previews display the `Orient` button,
while material and blueprint previews hide it through
`MainWindowViewModel.ShowOrientationButton`
(`WaywardBeyond.AssetStudio/ViewModels/MainWindowViewModel.cs:654` and
`WaywardBeyond.AssetStudio/Views/InspectorPaneView.axaml:233`).

The preview is unlit. The fragment shader selects a vertex-color passthrough mode,
a standalone 2D texture, a reference sprite texture, or the block albedo array
and multiplies albedo by the baked vertex tint and light
(`WaywardBeyond.AssetStudio/Rendering/PreviewShaderSource.cs:32`):

```glsl
if (uTextureMode == 2) { fragColor = vColor; return; }
vec4 albedo = uTextureMode == 1
    ? texture(uTexture2D, vUv.xy)
    : (uTextureMode == 3 ? texture(uReferenceTexture2D, vUv.xy) : texture(uAlbedo, vUv));
if (uAlphaCutoff > 0.0 && albedo.a < uAlphaCutoff) discard;
fragColor = vec4(albedo.rgb * vColor.rgb * vColor.a, albedo.a);
```

The mesher bakes light into the vertex color alpha
(`WaywardBeyond.Meshing/MeshPostPass.cs:366`), and the game
shader consumes it the same way
(`WaywardBeyond.Client/assets/shaders/lightedArray.glsl:52`). The tool needs no
lighting math.

Texture layers use the natural-sorted albedo order
(`WaywardBeyond.Client/Graphics/PBRTextureArraysParser.cs:80`). The tool builds
the same array from the PNG files.

Preview surfaces:

| Surface | Behavior |
|---|---|
| Brick with one shape | Orbit, orientation cycling |
| Brick with `Connected = true` | 16-mask connected showcase (`ConnectedBrickShowcaseBuilder` and `RegionMeshBuilder`), orbit |
| Brick with `Shape = Any` | Every shape in a labeled grid, shared orientation |
| Material | Textured 3D preview (`PreviewGeometryFactory` and `StandaloneTextureLoader`) with `Sphere` (default), `Plane`, and `Cube` shape selector (`MaterialPreviewShape` at `WaywardBeyond.AssetStudio/Models/MaterialPreviewShape.cs:6`) |
| Item view model | First-person framing preset, real standalone texture (`StandaloneTextureLoader`), orbit, reset |
| Item world model | Unit-cube framing preset, real standalone texture (`StandaloneTextureLoader`), orbit, reset |
| Paint | Color swatch for solid paints. Resolved block albedo layer over a transparency checkerboard for texture paints, with a magenta missing-layer badge when unresolved |
| Blueprint | Decoded voxel object with brick and paint palettes, masked alpha cutouts, orbit |
| Body | 2D sprite preview with frame slider and stepper controls |
| Recipe | Live crafting-flow diagram (`PreviewSurfaceMode.RecipeFlow`) showing clickable target tag, trigger mode, input and output item/tag cards with icons, counts, slot groups, drop chances, duration, power delta, throughput, and unresolved-reference warnings |

`PreviewViewportControl.DrawPaintSwatch`
(`WaywardBeyond.AssetStudio/Controls/PreviewViewportControl.cs:705`) renders
texture paints with nearest-neighbor sampling and resolves live variant edits
through `StudioGlPreviewController.ResolveBlockAlbedoLayerPath`
(`WaywardBeyond.AssetStudio/Services/StudioGlPreviewController.cs:353`).
`GlViewport.SetMaterialPreview`
(`WaywardBeyond.AssetStudio/Rendering/GlViewport.cs:301`) and
`PreviewViewportControl` render material previews as a UV sphere
(`PreviewGeometryFactory.CreateTexturedSphere` at
`WaywardBeyond.AssetStudio/Rendering/PreviewGeometryFactory.cs:88`), quad plane
(`CreateTexturedPlane` at `:163`), or unit cube (`CreateTexturedCube` at `:13`).
Material shaders are ignored. A missing texture shows the magenta placeholder.
A preview failure does not block editing. Headless users validate with the CLI.

### Item model visual editor

The View Model and World Model headers on `ItemFormView`
(`WaywardBeyond.AssetStudio/Views/Forms/ItemFormView.axaml.cs:8`) each expose an
`Edit Visually` button inside `CollapsibleSectionCard.HeaderAction`
(`WaywardBeyond.AssetStudio/Controls/CollapsibleSectionCard.cs:40`). Activating
either button opens a full-workbench editor (`ItemModelEditorViewModel` at
`WaywardBeyond.AssetStudio/ViewModels/ItemModelEditorViewModel.cs:15` and
`ItemModelEditorView` at
`WaywardBeyond.AssetStudio/Views/ItemModelEditorView.axaml.cs:12`) while keeping
the top Undo, Redo, Save, and F1 command bars and the bottom zoom bar visible.
`Back to Item` and `Esc` exit to the form and keep all live draft edits.

The editor mirrors the runtime transform order
`Scale * YawPitchRoll(Y, X, Z) * Translation`
(`WaywardBeyond.AssetStudio/Rendering/ModelTransformMath.cs:8` and
`WaywardBeyond.Client/Systems/PlayerViewModelSystem.cs:21`). Switching modes in
the sub-header toggles between two views:

- **View Model mode.** Locks `PreviewCamera`
  (`WaywardBeyond.AssetStudio/Rendering/PreviewCamera.cs:7`) to the eye origin
  looking down `-Z`, adjusts vertical FOV across `60` to `120` degrees with a
  default of `90` (`WaywardBeyond.Client/UI/Settings/SettingsDefaults.cs:14`),
  frames the viewport with `AspectRatioFrameControl`
  (`WaywardBeyond.AssetStudio/Controls/AspectRatioFrameControl.cs:8`) in `16:9`,
  `16:10`, `4:3`, or `Fill`, and overlays a center reticle.
- **World Model mode.** Uses an orbit camera and renders a `1.0 m` origin floor
  grid centered on `(0, 0, 0)` beside a standing `1.8 m` human-scale body
  sprite at `(0, 0, -1)` (`WaywardBeyond.Gameplay/CharacterDefaults.cs:56`).
  `WorkspaceModelReferenceResolver.ResolveDeterministicBodySprite`
  (`WaywardBeyond.AssetStudio/Services/WorkspaceModelReferenceResolver.cs:15`)
  picks the first body sorted ordinally by ID that has an existing sprite image
  on disk regardless of state.

Both modes provide sliders, numeric boxes, per-group and per-axis X/Y/Z reset
buttons (`ItemModelEditorViewModel.ResetPosition` at
`WaywardBeyond.AssetStudio/ViewModels/ItemModelEditorViewModel.cs:726` and
`ResetAxis` at `:768`), icon-only Copy and Paste actions backed by
`StudioTransformClipboard`
(`WaywardBeyond.AssetStudio/Models/StudioTransformClipboard.cs:7` and
`WaywardBeyond.AssetStudio/Views/ItemModelEditorView.axaml:457`),
copy-between-models, transform reset, and interactive 3D gizmos in OpenGL
(`ModelEditorViewport` at
`WaywardBeyond.AssetStudio/Rendering/ModelEditorViewport.cs:17`) and software
fallback (`ModelEditorFallbackControl` at
`WaywardBeyond.AssetStudio/Controls/ModelEditorFallbackControl.cs:18`).
`ModelEditorViewport` isolates its OpenGL state from the inspector `GlViewport`,
tracks uploaded GPU textures across sessions, and restores live rendering if a
later frame succeeds after a transient failure. Shortcuts
switch gizmo tools (`W` Move, `E` Rotate, `R` Scale, `Q` Local or World) with
optional snap (`0.05 m`, `5` degrees, `0.05` scale). Each completed gizmo drag
commits one atomic entry to `StudioUndoStack`, and saving persists changes
losslessly through `AssetTomlEditApplier.ApplyItemModelSectionEdits`
(`WaywardBeyond.AssetStudio/Services/AssetTomlEditApplier.cs:116`).

## Versioning

`ContentVersion.CURRENT` covers definition schemas. It sits next to
`DataVersion`, which covers save records only
(`WaywardBeyond.Data/DataVersion.cs:7`).

Pack metadata lives in `pack.toml` at the pack root, outside `assets/`. It holds
`ID`, `Namespace`, `ContentVersion`, `ToolVersion`, and `CreatedAt`. `new pack`
writes `pack.toml` at creation. Saving edits never silently rewrites
`pack.toml`. Only an explicit upgrade action or staged export updates
`ContentVersion` and `ToolVersion`.

| Case | Behavior |
|---|---|
| Pack version is newer than the tool | Read-only, with a reason |
| Pack version is older than the tool | Warning, full editing |
| Pack has no metadata | Unversioned warning |
| Repo workspace | No metadata, no check |

Packs are a Wayward Beyond concept layered over Shoal modules. The tool does not
read assemblies. Shoal only exposes the loaded module roots through
`ModuleCatalog`, so it stays unaware of `pack.toml` and `ContentVersion`.
Wayward Beyond reads `pack.toml` at startup for every loaded module. A missing
file is silent. A version mismatch logs a warning and the pack still loads.

| At load | Behavior |
|---|---|
| Pack version equals `ContentVersion.CURRENT` | No warning |
| Pack version is older or newer | Warning, load continues |
| No `pack.toml` | No check |
| Unreadable `pack.toml` | Warning, load continues |

## Export

Export runs full validation first. Fatal and broken findings block it. Warnings
need confirmation.

Export stages a copy in a temporary directory. It writes `pack.toml` in the stage
and archives the staged pack. The working tree does not change.

`PackArchiveFormat.Auto` (`WaywardBeyond.AssetTools/Packs/PackArchiveFormat.cs:6`)
resolves to `.zip` on all platforms, producing `<pack-id>-<contentVersion>.zip`.
Selecting `.tar.gz` explicitly produces `<pack-id>-<contentVersion>.tar.gz`.
Export does not upload and does not touch git.

## About and diagnostics

About shows the tool version, content version, workspace mode and roots, OS and
architecture, GL vendor and renderer, Avalonia version, and the log path.

Copy-diagnostics writes a zip. It holds `about.json`, the log tail, the
validation summary, and recent errors. It excludes content files and any path
outside the workspace roots.

## Blueprints

Blueprints are binary `BlueprintData` records
(`WaywardBeyond.Data/Nsd/blueprints.nsd:16`). The public API is
`IBlueprintStorage` and `FileBlueprintStorage` in
`WaywardBeyond.Data/Blueprints/`.

Limits are 64 voxels per edge, 65,536 voxels, 4 MiB per file, and 64 characters
per name (`WaywardBeyond.Data/Blueprints/FileBlueprintStorage.cs:25`).

The tool manages pack blueprints. It lists, previews, renames, duplicates, and
deletes them. It imports and exports with the player library, and re-encodes
palettes through `VoxelCodec` on import.

The tree lists pack blueprints by relative path. The center shows the 3D preview
and the metadata form (`BlueprintFormView` at
`WaywardBeyond.AssetStudio/Views/Forms/BlueprintFormView.axaml.cs:8`). The name
is editable. Size, brick count, and timestamps are read-only.

`StudioGlPreviewController.SyncBlueprintPreview`
(`WaywardBeyond.AssetStudio/Services/StudioGlPreviewController.cs:704`) decodes
brick and paint palettes through `VoxelCodec.Decode` using
`StudioPreviewBrickDatabase`
(`WaywardBeyond.AssetStudio/Services/StudioPreviewBrickDatabase.cs:12`) and
`StudioPreviewPaintDatabase`
(`WaywardBeyond.AssetStudio/Services/StudioPreviewPaintDatabase.cs:11`), meshes
regions through `RegionMeshBuilder`, and renders masked alpha cutouts in
`GlViewport.SetBlueprintPreview`
(`WaywardBeyond.AssetStudio/Rendering/GlViewport.cs:344`).
`StudioGlPreviewController.TryBuildBlueprintPreviewMesh`
(`WaywardBeyond.AssetStudio/Services/StudioGlPreviewController.cs:609`) reuses
the same mesher normalized to a unit cube to render 3D View Model and World
Model previews for `PlaceableType.Blueprint` items. Blueprint previews
hide the `Orient` button (`MainWindowViewModel.ShowOrientationButton` at
`WaywardBeyond.AssetStudio/ViewModels/MainWindowViewModel.cs:654`).
`AssetSeedProjector.ExtractBlueprintPalette`
(`WaywardBeyond.AssetStudio/Services/AssetSeedProjector.cs:1777`) counts voxel
and face usages across chunks so `BlueprintFormView` and the inspector
`USED BRICKS` section display the clickable Brick and Paint Palette
(`BlueprintPaletteItem` at
`WaywardBeyond.AssetStudio/Models/BlueprintPaletteItem.cs:6`) with voxel counts,
resolved status, and direct asset links.

The toolbar holds duplicate, delete, import, and export. Delete takes two steps.
Import starts at the player library and keeps the name.

Rename changes the display name only. It does not move the file, because the path
is the pack ID. A separate move action warns that the ID changes. There are no
generated thumbnails in v1.

The tool does not author voxels. The in-game projector does that. See issue
0004.

## Tool localization

- There is one `Strings.resx` per assembly. The core catalog is shared by the CLI
  and the app.
- Keys follow `Area.Element`, for example `ItemForm.Title`.
- The typed accessor uses the built-in `ResXFileCodeGenerator`. There is no
  custom generator.
- The culture follows the OS. A settings override wins. The fallback is English.
- A language change applies at startup in v1.
- A `qps-ploc` pseudo-locale ships in debug builds. It expands every string by
  hand.
- There is no plural engine and no RTL in v1. Layouts must not assume fixed
  widths.
- The tool locale is independent from the content locale it edits.

## Testing

| Layer | Check |
|---|---|
| Round-trip | Parse every shipped file, write no edits, compare bytes |
| Mutations | Golden text for add, remove, append, reorder, and enum change |
| Invalid content | Bad tokens survive an unrelated edit |
| Corruption | Truncated files, duplicate keys, and wrong table types |
| Extraction | Per-brick mesh hashes before and after the move |
| Schema | Definition fingerprint test and `ContentVersion` |
| View models | Avalonia.Headless |
| Pixels | `SceneRenderer` under Xvfb with llvmpipe |
| CLI | `roundtrip --check` over the workspace |

## Packaging

- Artifacts are self-contained folders. Windows uses `.zip`. Linux uses `.tar.gz`.
- Build bundles with `tools/package-asset-studio.sh` on Linux or
  `tools/package-asset-studio.ps1` on Windows.
- There is no single-file build, no installer, and no auto-update.
- Linux needs `libgl1`, `libx11-6`, `libfontconfig1`, and `libgtk-3-0`.
- See About and diagnostics for version and log details.

## Accepted limitations

- No module management UI in Asset Studio. Content packs load from
  `<DataRoot>/packs` when enabled in `<DataRoot>/config/modules.toml`.
- No voxel authoring.
- No particle TOML or CSV name tag editing.
- No unused localization key detection.
- No shader fidelity in previews.
- No macOS and no arm64.
- No git integration.
- No telemetry.
- The game warns on pack version skew but does not enforce it.

## Phases

| Phase | Deliverable | Gate | Status |
|---|---|---|---|
| P0 | Visibility changes, `ContentVersion` | Build and tests green | Done |
| P1 | `WaywardBeyond.Meshing` extraction | Mesh hashes match | Done |
| P2 | CLI validator | Shipped content report | Done |
| P3 | GL vertical slice | Xvfb golden test | Done |
| P4 | Edit engine and drafts | Round-trip suite | Done |
| P5 | Shell, forms, search, previews | Manual review | Done |
| P6 | Writes, refactors, packs, blueprints | Round-trip and pack tests | Done |
| P7 | Packaging, install locator, docs | Clean-machine run | Done |

## Source of truth

- TOML options and writer: `Swordfish.Library/Serialization/Toml/Toml.cs:7`
- Asset overwrite by load order: `Swordfish.Library/Collections/VirtualAssetDatabase.cs:75`
- File shadowing by mount order: `Swordfish.Library/IO/VirtualFileSystem.cs:101`
- Module discovery and merge: `Shoal/DependencyInjection/ConfigurationProvider.cs:17`, `Shoal/Modularity/ModuleResolutionOptions.cs:4`
- Wayward Beyond user packs root: `WaywardBeyond.Config/PackModuleResolution.cs:19`
- Module asset mount: `Shoal/AppEngine.cs:350`
- Module loaded callback, including content-only modules: `Shoal/Modularity/ModulesLoader.cs:208`
- Loaded module catalog: `Shoal/Modularity/ModuleCatalog.cs:4`, `Shoal/Modularity/LoadedModule.cs:9`
- Definition visibility: `WaywardBeyond.Bricks/BrickDefinition.cs:6`
- Material definition: `Swordfish/IO/MaterialDefinition.cs:6`
- OBJ parser: `Swordfish/IO/ObjParser.cs:8`
- Content version: `WaywardBeyond.Data/ContentVersion.cs:7`
- Runtime pack metadata and version check: `WaywardBeyond.Data/Packs/ContentPackMetadata.cs:7`, `WaywardBeyond.Data/Packs/ContentPackVersionChecker.cs:14`
- Save data version: `WaywardBeyond.Data/DataVersion.cs:7`
- Shared meshing types: `WaywardBeyond.Meshing/PreviewMesh.cs:6`, `WaywardBeyond.Meshing/ITextureLayerLookup.cs:4`
- Mesh vertex light: `WaywardBeyond.Meshing/MeshPostPass.cs:366`
- Texture layer fallback: `WaywardBeyond.Meshing/VoxelSurfaceCache.cs:390`
- Brick and region preview builders: `WaywardBeyond.Meshing/BrickPreviewMeshBuilder.cs:11`, `WaywardBeyond.Meshing/RegionMeshBuilder.cs:12`
- Shader light consumption: `WaywardBeyond.Client/assets/shaders/lightedArray.glsl:52`
- Texture layer order: `WaywardBeyond.Client/Graphics/PBRTextureArraysParser.cs:80`
- Shape enumeration: `WaywardBeyond.Bricks/BrickShape.cs:3`
- Blueprint format: `WaywardBeyond.Data/Nsd/blueprints.nsd:16`
- Blueprint storage: `WaywardBeyond.Data/Blueprints/IBlueprintStorage.cs:7`
- Blueprint limits: `WaywardBeyond.Data/Blueprints/FileBlueprintStorage.cs:25`
- Crash supervision: `Shoal/CrashHandling/CrashSupervisor.cs:13`
- Lossless TOML document and formatter: `WaywardBeyond.AssetTools/Documents/TomlDocument.cs:9`, `WaywardBeyond.AssetTools/Documents/TomlFormatter.cs:9`
- Draft store and atomic saves: `WaywardBeyond.AssetTools/Storage/DraftStore.cs:11`, `WaywardBeyond.AssetTools/Storage/AtomicFileSaver.cs:9`, `WaywardBeyond.AssetTools/Storage/MultiFileSaver.cs:11`
- Settings and portable paths: `WaywardBeyond.AssetTools/Settings/ToolDataPaths.cs:6`, `WaywardBeyond.AssetTools/Settings/ToolSettings.cs:6`, `WaywardBeyond.AssetTools/Settings/ToolSettingsStore.cs:11`
- Search ranking and navigation stack: `WaywardBeyond.AssetTools/Search/CommandPaletteSearchIndex.cs:8`, `WaywardBeyond.AssetTools/Search/NavigationHistory.cs:8`
- Tool localization: `WaywardBeyond.AssetTools/Localization/ToolLocalization.cs:9`
- Workspace loader, dual parser, and install locator: `WaywardBeyond.AssetTools/Workspace/WorkspaceLoader.cs:14`, `WaywardBeyond.AssetTools/Workspace/DualTomlParser.cs:9`, `WaywardBeyond.AssetTools/Workspace/InstallLocator.cs:10`
- Validation engine, quick fixes, and bulk actions: `WaywardBeyond.AssetTools/Validation/WorkspaceValidator.cs:16`, `WaywardBeyond.AssetTools/Validation/ValidationFixKind.cs:6`, `WaywardBeyond.AssetTools/Validation/BulkFixResult.cs:6`, `WaywardBeyond.AssetTools/Validation/ValidationBulkActions.cs:15`
- Asset scaffolding, cloning, ID allocation, deletion, rename planning, and two-pane tag editor: `WaywardBeyond.AssetTools/Editing/AssetScaffolder.cs:10`, `WaywardBeyond.AssetTools/Editing/AssetCloner.cs:15`, `WaywardBeyond.AssetTools/Editing/AssetIdAllocator.cs:10`, `WaywardBeyond.AssetTools/Editing/AssetClipboardPayload.cs:8`, `WaywardBeyond.AssetTools/Editing/AssetCloneResult.cs:8`, `WaywardBeyond.AssetTools/Editing/AssetDeleter.cs:11`, `WaywardBeyond.AssetTools/Editing/RenamePlanner.cs:11`, `WaywardBeyond.AssetTools/Editing/TagDocumentModel.cs:10`
- Pack metadata, export, archive format, diagnostics, and pack blueprints: `WaywardBeyond.AssetTools/Packs/PackMetadata.cs:11`, `WaywardBeyond.AssetTools/Packs/PackExporter.cs:16`, `WaywardBeyond.AssetTools/Packs/PackArchiveFormat.cs:6`, `WaywardBeyond.AssetTools/Diagnostics/DiagnosticsBundleWriter.cs:14`, `WaywardBeyond.AssetTools/Blueprints/PackBlueprintManager.cs:14`
- CLI commands: `WaywardBeyond.AssetTools.Cli/CliRunner.cs:15`
- Desktop entry, shell view model, and workspace services: `WaywardBeyond.AssetStudio/Program.cs:15`, `WaywardBeyond.AssetStudio/App.axaml.cs:14`, `WaywardBeyond.AssetStudio/ViewModels/MainWindowViewModel.cs:20`, `WaywardBeyond.AssetStudio/ViewModels/StartScreenViewModel.cs:12`, `WaywardBeyond.AssetStudio/ViewModels/DeleteRecentWorkspaceDialogViewModel.cs:10`, `WaywardBeyond.AssetStudio/ViewModels/ItemModelEditorViewModel.cs:15`, `WaywardBeyond.AssetStudio/ViewModels/StudioFieldViewModel.cs:11`, `WaywardBeyond.AssetStudio/Services/IStudioWorkspaceService.cs:8`, `WaywardBeyond.AssetStudio/Services/AssetToolsStudioWorkspaceService.cs:26`, `WaywardBeyond.AssetStudio/Services/AssetSeedProjector.cs:24`, `WaywardBeyond.AssetStudio/Services/AssetTomlEditApplier.cs:12`, `WaywardBeyond.AssetStudio/Services/WorkspaceModelReferenceResolver.cs:15`, `WaywardBeyond.AssetStudio/Services/IWorkspacePathPicker.cs:6`, `WaywardBeyond.AssetStudio/Services/DesktopWorkspacePathPicker.cs:10`, `WaywardBeyond.AssetStudio/Services/WorkspaceFileWatcher.cs:8`, `WaywardBeyond.AssetStudio/Services/StudioUndoStack.cs:6`, `WaywardBeyond.AssetStudio/Services/ThumbnailCache.cs:10`, `WaywardBeyond.AssetStudio/Services/MeshThumbnailRenderer.cs:13`
- Workspace image, reference, clipboard, and visual model types: `WaywardBeyond.AssetStudio/Models/WorkspaceImageEntry.cs:6`, `WaywardBeyond.AssetStudio/Models/BlueprintPaletteItem.cs:6`, `WaywardBeyond.AssetStudio/Models/ReferenceCandidate.cs:6`, `WaywardBeyond.AssetStudio/Models/BodyReferenceSpriteInfo.cs:6`, `WaywardBeyond.AssetStudio/Models/ResolvedItemModelVisual.cs:8`, `WaywardBeyond.AssetStudio/Models/ItemModelEditorMode.cs:4`, `WaywardBeyond.AssetStudio/Models/ItemModelAspectPreset.cs:4`, `WaywardBeyond.AssetStudio/Models/MaterialPreviewShape.cs:6`, `WaywardBeyond.AssetStudio/Models/StudioTransformClipboard.cs:7`
- Interactive controls, forms, dialogs, themes, and empty-state views: `WaywardBeyond.AssetStudio/Themes/StudioTheme.axaml:1`, `WaywardBeyond.AssetStudio/Themes/StudioStyles.axaml:1`, `WaywardBeyond.AssetStudio/Controls/BodySpritePickerControl.axaml.cs:15`, `WaywardBeyond.AssetStudio/Controls/ReferencePickerControl.axaml.cs:8`, `WaywardBeyond.AssetStudio/Controls/StudioColorPickerControl.axaml.cs:15`, `WaywardBeyond.AssetStudio/Controls/CollapsibleSectionCard.cs:11`, `WaywardBeyond.AssetStudio/Controls/AspectRatioFrameControl.cs:8`, `WaywardBeyond.AssetStudio/Controls/PreviewViewportControl.cs:20`, `WaywardBeyond.AssetStudio/Controls/ModelEditorFallbackControl.cs:18`, `WaywardBeyond.AssetStudio/Views/StartScreenView.axaml:1`, `WaywardBeyond.AssetStudio/Views/Dialogs/DeleteRecentWorkspaceDialog.axaml:1`, `WaywardBeyond.AssetStudio/Views/TagEditorView.axaml.cs:15`, `WaywardBeyond.AssetStudio/Views/LocalizationEditorView.axaml.cs:10`, `WaywardBeyond.AssetStudio/Views/ItemModelEditorView.axaml.cs:12`, `WaywardBeyond.AssetStudio/Views/Forms/ItemFormView.axaml.cs:8`, `WaywardBeyond.AssetStudio/Views/Forms/SkillFormView.axaml:1`, `WaywardBeyond.AssetStudio/Views/Forms/BlueprintFormView.axaml.cs:8`, `WaywardBeyond.AssetStudio/Views/ValidationDockView.axaml:1`, `WaywardBeyond.AssetStudio/Views/EditorHostView.axaml.cs:8`, `WaywardBeyond.AssetStudio/Views/WorkspaceExplorerView.axaml.cs:13`, `WaywardBeyond.AssetStudio/Views/InspectorPaneView.axaml.cs:10`
- GL preview controller, preview databases, renderer, gizmos, shaders, textures, and showcase builders: `WaywardBeyond.AssetStudio/Services/StudioGlPreviewController.cs:25`, `WaywardBeyond.AssetStudio/Services/StudioPreviewBrickDatabase.cs:12`, `WaywardBeyond.AssetStudio/Services/StudioPreviewPaintDatabase.cs:11`, `WaywardBeyond.AssetStudio/Rendering/SceneRenderer.cs:13`, `WaywardBeyond.AssetStudio/Rendering/PreviewCamera.cs:7`, `WaywardBeyond.AssetStudio/Rendering/ModelTransformMath.cs:8`, `WaywardBeyond.AssetStudio/Rendering/TransformGizmoController.cs:8`, `WaywardBeyond.AssetStudio/Rendering/TransformGizmoMeshBuilder.cs:10`, `WaywardBeyond.AssetStudio/Rendering/TransformGizmoMode.cs:4`, `WaywardBeyond.AssetStudio/Rendering/TransformGizmoSpace.cs:4`, `WaywardBeyond.AssetStudio/Rendering/TransformGizmoAxis.cs:4`, `WaywardBeyond.AssetStudio/Rendering/PreviewTextureMode.cs:4`, `WaywardBeyond.AssetStudio/Rendering/ModelEditorRenderState.cs:8`, `WaywardBeyond.AssetStudio/Rendering/GlViewport.cs:21`, `WaywardBeyond.AssetStudio/Rendering/ModelEditorViewport.cs:17`, `WaywardBeyond.AssetStudio/Rendering/PreviewShaderSource.cs:4`, `WaywardBeyond.AssetStudio/Rendering/PbrAlbedoArrayLoader.cs:12`, `WaywardBeyond.AssetStudio/Rendering/StandaloneTextureLoader.cs:10`, `WaywardBeyond.AssetStudio/Rendering/PreviewGeometryFactory.cs:10`, `WaywardBeyond.AssetStudio/Rendering/ConnectedBrickShowcaseBuilder.cs:10`
- Game runtime view model and standing height parity sources: `WaywardBeyond.Client/Systems/PlayerViewModelSystem.cs:21`, `WaywardBeyond.Client/UI/Settings/SettingsDefaults.cs:14`, `WaywardBeyond.Gameplay/CharacterDefaults.cs:56`
- Packaging scripts: `tools/package-asset-studio.sh:1`, `tools/package-asset-studio.ps1:1`

## Tests that pin this

Game, schema, and meshing parity tests:

- `WaywardBeyond.Tests/ContentVersionTests.cs`
- `WaywardBeyond.Tests/ContentPackVersionTests.cs`
- `WaywardBeyond.Tests/BrickMeshingHashParityTests.cs`
- `WaywardBeyond.Tests/Blueprints/FileBlueprintStorageTests.cs`
- `WaywardBeyond.Voxels.Tests/VoxelRegionTests.cs`
- `WaywardBeyond.Tests/BrickPreviewTests.cs`
- `WaywardBeyond.Tests/ShapedBrickTextureTests.cs`
- `WaywardBeyond.Tests/PlayerViewModelTests.cs`
- `WaywardBeyond.Tests/ItemPlaceablesTests.cs`
- `WaywardBeyond.Tests/LocalizedTagsTests.cs`

Asset tools, CLI, and Asset Studio tests:

- `WaywardBeyond.AssetTools.Tests/Documents/TomlRoundTripTests.cs`
- `WaywardBeyond.AssetTools.Tests/Documents/TomlMutationGoldenTests.cs`
- `WaywardBeyond.AssetTools.Tests/Documents/TomlInvalidAndCorruptionTests.cs`
- `WaywardBeyond.AssetTools.Tests/Storage/DraftAndAtomicSaveTests.cs`
- `WaywardBeyond.AssetTools.Tests/Settings/SettingsAndLocalizationTests.cs`
- `WaywardBeyond.AssetTools.Tests/Search/SearchAndNavigationTests.cs`
- `WaywardBeyond.AssetTools.Tests/Workspace/WorkspaceAndLocatorTests.cs`
- `WaywardBeyond.AssetTools.Tests/Validation/ValidationEngineTests.cs`
- `WaywardBeyond.AssetTools.Tests/Validation/ValidationQuickFixTests.cs`
- `WaywardBeyond.AssetTools.Tests/Editing/EditingAndTagsTests.cs`
- `WaywardBeyond.AssetTools.Tests/Editing/AssetClonerTests.cs`
- `WaywardBeyond.AssetTools.Tests/Editing/AssetDeleterTests.cs`
- `WaywardBeyond.AssetTools.Tests/Packs/PacksDiagnosticsAndBlueprintsTests.cs`
- `WaywardBeyond.AssetTools.Tests/Cli/CliRunnerTests.cs`
- `WaywardBeyond.AssetStudio.Tests/GlSceneRendererTests.cs`
- `WaywardBeyond.AssetStudio.Tests/ItemModelEditorTests.cs`
- `WaywardBeyond.AssetStudio.Tests/StudioInteractionTests.cs`
- `WaywardBeyond.AssetStudio.Tests/StudioIssueRegressionTests.cs`
- `WaywardBeyond.AssetStudio.Tests/StudioStage2IntegrationTests.cs`
- `WaywardBeyond.AssetStudio.Tests/StartScreenRecentDeletionTests.cs`
- `WaywardBeyond.AssetStudio.Tests/RealWorkspaceIntegrationTests.cs`
- `WaywardBeyond.AssetStudio.Tests/VisualCaptureTests.cs`
