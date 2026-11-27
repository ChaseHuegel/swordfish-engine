using System;
using System.Numerics;
using System.Threading;
using Krill;
using WaywardBeyond.Bricks;
using WaywardBeyond.Config;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Networking;
using WaywardBeyond.Gameplay;
using WaywardBeyond.Voxels;

namespace WaywardBeyond.Client.Systems;

/// <summary>
/// Shared state between <see cref="RegionToolService"/>, <see cref="RegionPreviewSystem"/>,
/// <see cref="RegionToolRadial"/>, <see cref="ControlHints"/>, and the Blueprint Library UI.
/// </summary>
internal sealed class RegionToolState(GameplayConfig? gameplayConfig = null)
{
    private readonly Lock _gate = new();
    private readonly GameplayConfig? _gameplayConfig = gameplayConfig;

    private int MaxRegionEdge => _gameplayConfig?.Regions.MaxEdge.Get() ?? RegionLimits.MAX_REGION_EDGE;
    private int MaxRegionVolume => _gameplayConfig?.Regions.MaxVolume.Get() ?? RegionLimits.MAX_REGION_VOLUME;

    private RegionToolMode _mode = RegionToolMode.Select;
    private Uuid _selectionEntityUuid = Uuid.Null;
    private bool _hasAnchorA;
    private bool _hasAnchorB;
    private Int3 _anchorA;
    private Int3 _anchorB;

    private bool _hasClipboard;
    private VoxelRegionData _clipboardRegion;
    private int _clipboardBrickCount;
    private int _clipboardRevision;
    private string _clipboardName = string.Empty;

    private bool _hasHeldBlueprint;
    private string _heldBlueprintItemId = string.Empty;
    private string _heldBlueprintId = string.Empty;
    private VoxelRegionData _heldBlueprintRegion;
    private int _heldBlueprintRevision;
    private Orientation _heldBlueprintRotation = Orientation.Identity;
    private MirrorAxes _heldBlueprintMirror = MirrorAxes.None;
    private int _nextRevision;

    private Orientation _pasteRotation = Orientation.Identity;
    private MirrorAxes _pasteMirror = MirrorAxes.None;
    private bool _overwriteAir;
    private float _freeSpaceDistance = CharacterDefaults.REGION_FREE_DISTANCE_DEFAULT;

    private bool _previewVisible;
    private bool _previewValid;
    private PasteTargetKind _previewTargetKind;
    private Uuid _previewTargetEntity = Uuid.Null;
    private Int3 _previewOrigin;
    private Int3 _previewSize;
    private Vector3 _previewWorldPosition;
    private Quaternion _previewWorldOrientation = Quaternion.Identity;
    private Vector3 _previewWorldScale = Vector3.One;
    private Vector3 _previewLocalOffset;
    private string _previewInvalidErrorKey = string.Empty;

    private int _cachedRevision = -1;
    private Orientation _cachedRotation;
    private MirrorAxes _cachedMirror;
    private VoxelRegionData _cachedTransformed;

    private int _heldCachedRevision = -1;
    private Orientation _heldCachedRotation;
    private MirrorAxes _heldCachedMirror;
    private VoxelRegionData _heldCachedTransformed;

    /// <summary>Active sub-mode (Select or Paste).</summary>
    public RegionToolMode Mode
    {
        get { lock (_gate) return _mode; }
    }

    /// <summary>True when clipboard contains a copied or loaded blueprint region.</summary>
    public bool HasClipboard
    {
        get { lock (_gate) return _hasClipboard; }
    }

    /// <summary>True when a held blueprint placeable item is actively driving prefab placement.</summary>
    public bool IsBlueprintItemActive
    {
        get { lock (_gate) return _hasHeldBlueprint; }
    }

    /// <summary>Monotonically incremented whenever the clipboard content changes.</summary>
    public int ClipboardRevision
    {
        get { lock (_gate) return _clipboardRevision; }
    }

    /// <summary>Solid brick count of the current clipboard region.</summary>
    public int ClipboardBrickCount
    {
        get { lock (_gate) return _clipboardBrickCount; }
    }

    /// <summary>Optional display name of the current clipboard or loaded blueprint.</summary>
    public string ClipboardName
    {
        get { lock (_gate) return _clipboardName; }
    }

    /// <summary>Current 90-degree paste rotation for the projector clipboard.</summary>
    public Orientation PasteRotation
    {
        get { lock (_gate) return _pasteRotation; }
    }

    /// <summary>Current mirror axes mask applied before paste rotation for the projector clipboard.</summary>
    public MirrorAxes PasteMirror
    {
        get { lock (_gate) return _pasteMirror; }
    }

    /// <summary>Current 90-degree rotation for whichever placement source is active (blueprint item or projector clipboard).</summary>
    public Orientation ActivePlacementRotation
    {
        get { lock (_gate) return _hasHeldBlueprint ? _heldBlueprintRotation : _pasteRotation; }
    }

    /// <summary>Current mirror axes mask for whichever placement source is active (blueprint item or projector clipboard).</summary>
    public MirrorAxes ActivePlacementMirror
    {
        get { lock (_gate) return _hasHeldBlueprint ? _heldBlueprintMirror : _pasteMirror; }
    }

    /// <summary>When true, air cells inside the pasted region overwrite existing bricks on target structures.</summary>
    public bool OverwriteAir
    {
        get { lock (_gate) return _overwriteAir; }
    }

    /// <summary>Distance in front of the camera for free-space new-structure spawning.</summary>
    public float FreeSpaceDistance
    {
        get { lock (_gate) return _freeSpaceDistance; }
    }

    /// <summary>Localization key explaining why the active paste placement is invalid, or empty when valid.</summary>
    public string PreviewInvalidErrorKey
    {
        get { lock (_gate) return _previewInvalidErrorKey; }
    }

    /// <summary>Scale of the active paste preview target structure.</summary>
    public Vector3 PreviewWorldScale
    {
        get { lock (_gate) return _previewWorldScale; }
    }

    /// <summary>Sets anchor A on <paramref name="entityUuid"/>, resetting anchors if the target structure changed.</summary>
    public bool SetAnchorA(Uuid entityUuid, Int3 cell)
    {
        lock (_gate)
        {
            bool switchedEntity = _selectionEntityUuid != Uuid.Null && _selectionEntityUuid != entityUuid;
            if (switchedEntity)
            {
                _hasAnchorB = false;
                _anchorB = default;
            }

            _selectionEntityUuid = entityUuid;
            _anchorA = cell;
            _hasAnchorA = true;
            _mode = RegionToolMode.Select;
            return switchedEntity;
        }
    }

    /// <summary>Sets anchor B on <paramref name="entityUuid"/>. Fails if anchor A belongs to a different structure.</summary>
    public bool SetAnchorB(Uuid entityUuid, Int3 cell)
    {
        lock (_gate)
        {
            if (_hasAnchorA && _selectionEntityUuid != entityUuid)
            {
                return false;
            }

            _selectionEntityUuid = entityUuid;
            _anchorB = cell;
            _hasAnchorB = true;
            _mode = RegionToolMode.Select;
            return true;
        }
    }

    /// <summary>Clears active selection anchors without modifying the clipboard.</summary>
    public void ClearSelection()
    {
        lock (_gate)
        {
            _selectionEntityUuid = Uuid.Null;
            _hasAnchorA = false;
            _hasAnchorB = false;
            _anchorA = default;
            _anchorB = default;
        }
    }

    /// <summary>Reads current anchor points and whether a complete bounded box is active.</summary>
    public bool TryGetSelectionAnchors(
        out Uuid entityUuid,
        out bool hasAnchorA,
        out Int3 anchorA,
        out bool hasAnchorB,
        out Int3 anchorB,
        out Int3 min,
        out Int3 max,
        out bool withinLimits
    )
    {
        lock (_gate)
        {
            entityUuid = _selectionEntityUuid;
            hasAnchorA = _hasAnchorA;
            anchorA = _anchorA;
            hasAnchorB = _hasAnchorB;
            anchorB = _anchorB;

            if (_selectionEntityUuid == Uuid.Null || (!_hasAnchorA && !_hasAnchorB))
            {
                min = default;
                max = default;
                withinLimits = false;
                return false;
            }

            Int3 a = _hasAnchorA ? _anchorA : _anchorB;
            Int3 b = _hasAnchorB ? _anchorB : _anchorA;
            min = new Int3(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Min(a.Z, b.Z));
            max = new Int3(Math.Max(a.X, b.X), Math.Max(a.Y, b.Y), Math.Max(a.Z, b.Z));
            withinLimits = RegionLimits.IsValidBounds(min, max, MaxRegionEdge, MaxRegionVolume);
            return true;
        }
    }

    /// <summary>Gets the complete, limit-valid selection bounds on one entity.</summary>
    public bool TryGetCompleteSelection(out Uuid entityUuid, out Int3 min, out Int3 max)
    {
        lock (_gate)
        {
            if (_selectionEntityUuid == Uuid.Null || !_hasAnchorA || !_hasAnchorB)
            {
                entityUuid = Uuid.Null;
                min = default;
                max = default;
                return false;
            }

            min = new Int3(
                Math.Min(_anchorA.X, _anchorB.X),
                Math.Min(_anchorA.Y, _anchorB.Y),
                Math.Min(_anchorA.Z, _anchorB.Z));
            max = new Int3(
                Math.Max(_anchorA.X, _anchorB.X),
                Math.Max(_anchorA.Y, _anchorB.Y),
                Math.Max(_anchorA.Z, _anchorB.Z));
            entityUuid = _selectionEntityUuid;
            return RegionLimits.IsValidBounds(min, max, MaxRegionEdge, MaxRegionVolume);
        }
    }

    /// <summary>Stores a normalized region in the clipboard and resets transform state.</summary>
    public void SetClipboard(in VoxelRegionData region, int brickCount, string? name = null, bool enterPasteMode = false)
    {
        lock (_gate)
        {
            _clipboardRegion = region;
            _clipboardBrickCount = brickCount;
            _clipboardName = name ?? string.Empty;
            _hasClipboard = region.SizeX > 0 && region.SizeY > 0 && region.SizeZ > 0 && brickCount > 0;
            _clipboardRevision = ++_nextRevision;
            _cachedRevision = -1;
            _pasteRotation = Orientation.Identity;
            _pasteMirror = MirrorAxes.None;
            if (enterPasteMode && _hasClipboard)
            {
                _mode = RegionToolMode.Paste;
            }
        }
    }

    /// <summary>Returns whether the active held blueprint item matches <paramref name="itemId"/> and <paramref name="blueprintId"/>.</summary>
    public bool MatchesHeldBlueprint(string itemId, string blueprintId)
    {
        lock (_gate)
        {
            return _hasHeldBlueprint
                && string.Equals(_heldBlueprintItemId, itemId, StringComparison.Ordinal)
                && string.Equals(_heldBlueprintId, blueprintId, StringComparison.Ordinal);
        }
    }

    /// <summary>Activates prefab placement state for a held blueprint placeable item without disturbing the projector clipboard.</summary>
    public void SetHeldBlueprint(string itemId, string blueprintId, in VoxelRegionData region)
    {
        lock (_gate)
        {
            if (_hasHeldBlueprint
                && string.Equals(_heldBlueprintItemId, itemId, StringComparison.Ordinal)
                && string.Equals(_heldBlueprintId, blueprintId, StringComparison.Ordinal))
            {
                return;
            }

            _heldBlueprintItemId = itemId;
            _heldBlueprintId = blueprintId;
            _heldBlueprintRegion = region;
            _hasHeldBlueprint = region.SizeX > 0 && region.SizeY > 0 && region.SizeZ > 0;
            _heldBlueprintRevision = ++_nextRevision;
            _heldCachedRevision = -1;
            _heldBlueprintRotation = Orientation.Identity;
            _heldBlueprintMirror = MirrorAxes.None;
        }
    }

    /// <summary>Clears active held blueprint item placement state without modifying the projector clipboard.</summary>
    public void ClearHeldBlueprint()
    {
        lock (_gate)
        {
            if (!_hasHeldBlueprint)
            {
                return;
            }

            _hasHeldBlueprint = false;
            _heldBlueprintItemId = string.Empty;
            _heldBlueprintId = string.Empty;
            _heldBlueprintRegion = default;
            _heldCachedRevision = -1;
            _previewVisible = false;
        }
    }

    /// <summary>Tries to enter or leave Paste mode.</summary>
    public bool SetMode(RegionToolMode mode)
    {
        lock (_gate)
        {
            if (mode == RegionToolMode.Paste && !_hasClipboard)
            {
                return false;
            }

            _mode = mode;
            if (mode != RegionToolMode.Paste)
            {
                _previewVisible = false;
            }

            return true;
        }
    }

    /// <summary>Toggles between Select and Paste mode.</summary>
    public bool TogglePasteMode()
    {
        lock (_gate)
        {
            if (_mode == RegionToolMode.Paste)
            {
                _mode = RegionToolMode.Select;
                _previewVisible = false;
                return true;
            }

            if (!_hasClipboard)
            {
                return false;
            }

            _mode = RegionToolMode.Paste;
            return true;
        }
    }

    /// <summary>Rotates the active placement preview by 90 degrees around the local Y axis (yaw).</summary>
    public void RotateYaw(int steps = 1)
    {
        lock (_gate)
        {
            Quaternion step = Quaternion.CreateFromAxisAngle(Vector3.UnitY, steps * (MathF.PI * 0.5f));
            if (_hasHeldBlueprint)
            {
                _heldBlueprintRotation = new Orientation(step * _heldBlueprintRotation.ToQuaternion());
            }
            else
            {
                _pasteRotation = new Orientation(step * _pasteRotation.ToQuaternion());
            }
        }
    }

    /// <summary>Rotates the active placement preview by 90 degrees around the local X axis (pitch).</summary>
    public void RotatePitch(int steps = 1)
    {
        lock (_gate)
        {
            Quaternion step = Quaternion.CreateFromAxisAngle(Vector3.UnitX, steps * (MathF.PI * 0.5f));
            if (_hasHeldBlueprint)
            {
                _heldBlueprintRotation = new Orientation(step * _heldBlueprintRotation.ToQuaternion());
            }
            else
            {
                _pasteRotation = new Orientation(step * _pasteRotation.ToQuaternion());
            }
        }
    }

    /// <summary>Flips the mirror bit(s) for the requested axis/axes on the active placement preview.</summary>
    public MirrorAxes ToggleMirror(MirrorAxes axis)
    {
        lock (_gate)
        {
            if (_hasHeldBlueprint)
            {
                _heldBlueprintMirror ^= axis;
                return _heldBlueprintMirror;
            }

            _pasteMirror ^= axis;
            return _pasteMirror;
        }
    }

    /// <summary>Toggles whether air cells inside the paste region overwrite target bricks.</summary>
    public bool ToggleOverwriteAir()
    {
        lock (_gate)
        {
            _overwriteAir = !_overwriteAir;
            return _overwriteAir;
        }
    }

    /// <summary>Adjusts free-space spawn distance along the view ray.</summary>
    public void AdjustFreeSpaceDistance(float deltaMeters)
    {
        lock (_gate)
        {
            _freeSpaceDistance = Math.Clamp(
                _freeSpaceDistance + deltaMeters,
                CharacterDefaults.REGION_FREE_DISTANCE_MIN,
                CharacterDefaults.REGION_FREE_DISTANCE_MAX);
        }
    }

    /// <summary>Returns the raw untransformed clipboard region if present.</summary>
    public bool TryGetRawClipboard(out VoxelRegionData region, out int brickCount, out string name)
    {
        lock (_gate)
        {
            region = _clipboardRegion;
            brickCount = _clipboardBrickCount;
            name = _clipboardName;
            return _hasClipboard;
        }
    }

    /// <summary>Returns the cached transformed region for the current rotation and mirror flags.</summary>
    public bool TryGetTransformedClipboard(
        IBrickDatabase? brickDatabase,
        out VoxelRegionData transformed,
        out int revision,
        out Orientation rotation,
        out MirrorAxes mirror
    )
    {
        lock (_gate)
        {
            if (!_hasClipboard)
            {
                transformed = default;
                revision = _clipboardRevision;
                rotation = _pasteRotation;
                mirror = _pasteMirror;
                return false;
            }

            if (_cachedRevision != _clipboardRevision || _cachedRotation != _pasteRotation || _cachedMirror != _pasteMirror)
            {
                _cachedTransformed = VoxelRegionTransform.Transform(in _clipboardRegion, _pasteRotation, _pasteMirror, brickDatabase);
                _cachedRevision = _clipboardRevision;
                _cachedRotation = _pasteRotation;
                _cachedMirror = _pasteMirror;
            }

            transformed = _cachedTransformed;
            revision = _clipboardRevision;
            rotation = _pasteRotation;
            mirror = _pasteMirror;
            return true;
        }
    }

    /// <summary>Returns the cached transformed region for whichever placement source is active (held blueprint item or projector clipboard).</summary>
    public bool TryGetActivePlacementRegion(
        IBrickDatabase? brickDatabase,
        out VoxelRegionData transformed,
        out int revision,
        out Orientation rotation,
        out MirrorAxes mirror
    )
    {
        lock (_gate)
        {
            if (_hasHeldBlueprint)
            {
                if (_heldCachedRevision != _heldBlueprintRevision
                    || _heldCachedRotation != _heldBlueprintRotation
                    || _heldCachedMirror != _heldBlueprintMirror)
                {
                    _heldCachedTransformed = VoxelRegionTransform.Transform(
                        in _heldBlueprintRegion,
                        _heldBlueprintRotation,
                        _heldBlueprintMirror,
                        brickDatabase);
                    _heldCachedRevision = _heldBlueprintRevision;
                    _heldCachedRotation = _heldBlueprintRotation;
                    _heldCachedMirror = _heldBlueprintMirror;
                }

                transformed = _heldCachedTransformed;
                revision = _heldBlueprintRevision;
                rotation = _heldBlueprintRotation;
                mirror = _heldBlueprintMirror;
                return true;
            }
        }

        return TryGetTransformedClipboard(brickDatabase, out transformed, out revision, out rotation, out mirror);
    }

    /// <summary>Publishes the resolved paste placement pose computed by <see cref="RegionToolService"/>.</summary>
    public void SetPreviewPlacement(
        bool visible,
        bool valid,
        PasteTargetKind targetKind,
        Uuid targetEntity,
        Int3 origin,
        Int3 size,
        Vector3 worldPosition,
        Quaternion worldOrientation,
        Vector3 localOffset,
        string? invalidErrorKey = null,
        Vector3? worldScale = null
    )
    {
        lock (_gate)
        {
            _previewVisible = visible;
            _previewValid = valid;
            _previewTargetKind = targetKind;
            _previewTargetEntity = targetEntity;
            _previewOrigin = origin;
            _previewSize = size;
            _previewWorldPosition = worldPosition;
            _previewWorldOrientation = worldOrientation;
            _previewWorldScale = worldScale ?? Vector3.One;
            _previewLocalOffset = localOffset;
            _previewInvalidErrorKey = invalidErrorKey ?? string.Empty;
        }
    }

    /// <summary>Hides the active paste preview.</summary>
    public void HidePreview()
    {
        lock (_gate)
        {
            _previewVisible = false;
        }
    }

    /// <summary>Reads the current resolved paste placement pose.</summary>
    public bool TryGetPreviewPlacement(
        out bool valid,
        out PasteTargetKind targetKind,
        out Uuid targetEntity,
        out Int3 origin,
        out Int3 size,
        out Vector3 worldPosition,
        out Quaternion worldOrientation,
        out Vector3 localOffset
    )
    {
        lock (_gate)
        {
            valid = _previewValid;
            targetKind = _previewTargetKind;
            targetEntity = _previewTargetEntity;
            origin = _previewOrigin;
            size = _previewSize;
            worldPosition = _previewWorldPosition;
            worldOrientation = _previewWorldOrientation;
            localOffset = _previewLocalOffset;
            return _previewVisible;
        }
    }

    /// <summary>Clears world-bound selection, held blueprint state, and preview on disconnect while preserving the player's clipboard.</summary>
    public void ResetTransientOnDisconnect()
    {
        lock (_gate)
        {
            _selectionEntityUuid = Uuid.Null;
            _hasAnchorA = false;
            _hasAnchorB = false;
            _anchorA = default;
            _anchorB = default;
            _mode = RegionToolMode.Select;
            _hasHeldBlueprint = false;
            _heldBlueprintItemId = string.Empty;
            _heldBlueprintId = string.Empty;
            _heldBlueprintRegion = default;
            _heldCachedRevision = -1;
            _previewVisible = false;
        }
    }
}
