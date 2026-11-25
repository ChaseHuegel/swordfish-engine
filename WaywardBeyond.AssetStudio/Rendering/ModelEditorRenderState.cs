using System.Numerics;
using WaywardBeyond.Bricks;
using WaywardBeyond.Meshing;

namespace WaywardBeyond.AssetStudio.Rendering;

/// <summary>Snapshot of transform, camera, reference, and gizmo state rendered in the item model editor.</summary>
public readonly record struct ModelEditorRenderState
{
    /// <summary>Default empty render state showing a unit cube in ViewModel mode.</summary>
    public static ModelEditorRenderState Default { get; } = new()
    {
        ModePreset = PreviewCameraPreset.ItemViewModelFirstPerson,
        IsWorldModel = false,
        FovDegrees = 90f,
        Position = ModelTransformMath.DefaultViewModelPosition,
        RotationDegrees = ModelTransformMath.DefaultViewModelRotation,
        Scale = ModelTransformMath.DefaultViewModelScale,
        GizmoMode = TransformGizmoMode.Translate,
        GizmoSpace = TransformGizmoSpace.Local,
        SnapEnabled = false,
        UniformScale = true,
        ShowFloorPlane = true,
        ShowBodyReference = true,
        ReferenceBodyAspect = 0.6f,
        AlphaMode = BrickAlpha.Opaque,
        AlphaCutoff = 0.5f,
        TextureMode = PreviewTextureMode.BlockAlbedoArray,
    };

    /// <summary>Active camera preset identifier.</summary>
    public PreviewCameraPreset ModePreset { get; init; }

    /// <summary>True when editing the third-person world model instead of the first-person view model.</summary>
    public bool IsWorldModel { get; init; }

    /// <summary>Vertical field of view in degrees used by the first-person view model camera.</summary>
    public float FovDegrees { get; init; }

    /// <summary>Local translation in meters.</summary>
    public Vector3 Position { get; init; }

    /// <summary>Local Yaw(Y)-Pitch(X)-Roll(Z) rotation in degrees.</summary>
    public Vector3 RotationDegrees { get; init; }

    /// <summary>Local non-uniform or uniform scale factors.</summary>
    public Vector3 Scale { get; init; }

    /// <summary>Active 3D gizmo tool (Translate, Rotate, or Scale).</summary>
    public TransformGizmoMode GizmoMode { get; init; }

    /// <summary>Coordinate basis used for Translate and Rotate gizmos.</summary>
    public TransformGizmoSpace GizmoSpace { get; init; }

    /// <summary>True when interactive gizmo drags quantize to fixed increments.</summary>
    public bool SnapEnabled { get; init; }

    /// <summary>True when scale drags preserve axis proportions.</summary>
    public bool UniformScale { get; init; }

    /// <summary>True to render the 1.0 m floor reference plane at <c>(0, 0, 0)</c> in WorldModel mode.</summary>
    public bool ShowFloorPlane { get; init; }

    /// <summary>True to render the 1.8 m standing body sprite plane at <c>(0, 0, -1)</c> in WorldModel mode.</summary>
    public bool ShowBodyReference { get; init; }

    /// <summary>True when editing a read-only base asset (hides manipulation gizmos).</summary>
    public bool IsReadOnly { get; init; }

    /// <summary>Item mesh geometry to display.</summary>
    public VoxelMesh MeshGeometry { get; init; }

    /// <summary>Surface texture sampling mode for the item mesh.</summary>
    public PreviewTextureMode TextureMode { get; init; }

    /// <summary>Alpha mode of the item mesh.</summary>
    public BrickAlpha AlphaMode { get; init; }

    /// <summary>Alpha cutoff threshold when <see cref="AlphaMode"/> is <see cref="BrickAlpha.Masked"/>.</summary>
    public float AlphaCutoff { get; init; }

    /// <summary>True when back-face culling is disabled for the item mesh.</summary>
    public bool DoubleSided { get; init; }

    /// <summary>Optional standalone texture for OBJ + Material item visuals.</summary>
    public StandaloneTextureData? ItemStandaloneTexture { get; init; }

    /// <summary>Optional body ID label shown on the 1.8 m scale reference plane.</summary>
    public string? ReferenceBodyId { get; init; }

    /// <summary>Optional absolute path to the deterministic body sprite PNG for software fallback rendering.</summary>
    public string? ReferenceBodySpritePath { get; init; }

    /// <summary>Optional decoded body sprite RGBA pixels uploaded to GPU texture unit 2.</summary>
    public StandaloneTextureData? ReferenceBodyTexture { get; init; }

    /// <summary>Width-over-height aspect ratio of the body scale reference plane.</summary>
    public float ReferenceBodyAspect { get; init; }

    /// <summary>Accent hex tint used by the software fallback preview when no texture bitmap is loaded.</summary>
    public string? AccentHex { get; init; }
}
