namespace WaywardBeyond.AssetStudio.Rendering;

/// <summary>Identifies the camera framing preset applied to the 3D preview viewport.</summary>
public enum PreviewCameraPreset
{
    /// <summary>Standard three-quarter isometric orbit framing for a single brick.</summary>
    OrbitBrick,

    /// <summary>Wide elevated framing covering the six-shape grid for Shape = Any bricks.</summary>
    BrickShapeGrid,

    /// <summary>First-person held-item framing preset for item view models.</summary>
    ItemViewModel,

    /// <summary>Unit-cube centered framing preset for item world models.</summary>
    ItemWorldModel,

    /// <summary>Auto-framed orbit camera for multi-voxel blueprints.</summary>
    Blueprint,

    /// <summary>Front-biased three-quarter framing showcasing all 16 connected-texture masks.</summary>
    ConnectedShowcase,

    /// <summary>Exact first-person game camera at the origin looking down -Z with configurable FOV.</summary>
    ItemViewModelFirstPerson,

    /// <summary>Human-scale orbit framing showing the 1 m origin floor plane and 1.8 m standing body reference.</summary>
    ItemWorldModelEditor,
}
