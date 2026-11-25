namespace WaywardBeyond.AssetStudio.Rendering;

/// <summary>Selects how the shared preview shader sources fragment color for a mesh.</summary>
public enum PreviewTextureMode
{
    /// <summary>Samples the stitched PBR block texture array (<c>uAlbedo</c>, texture unit 0).</summary>
    BlockAlbedoArray = 0,

    /// <summary>Samples the primary standalone 2D texture (<c>uTexture2D</c>, texture unit 1).</summary>
    StandaloneTexture = 1,

    /// <summary>Uses interpolated RGBA vertex colors without sampling any texture.</summary>
    VertexColorOnly = 2,

    /// <summary>Samples the auxiliary reference 2D texture (<c>uReferenceTexture2D</c>, texture unit 2).</summary>
    ReferenceTexture = 3,
}
