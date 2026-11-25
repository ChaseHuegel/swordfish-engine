using WaywardBeyond.AssetStudio.Rendering;

namespace WaywardBeyond.AssetStudio.Models;

/// <summary>Describes the deterministic body sprite selected as the 1.8 m human-scale reference for WorldModel editing.</summary>
public readonly record struct BodyReferenceSpriteInfo(
    string BodyId,
    string StateName,
    string Direction,
    string AbsolutePath,
    StandaloneTextureData? Texture);
