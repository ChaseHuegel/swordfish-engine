namespace WaywardBeyond.Meshing;

/// <summary>Resolves bare albedo texture layer names to their zero-based texture-array layer indices.</summary>
public interface ITextureLayerLookup
{
    /// <summary>Returns the layer index for <paramref name="name"/>, or -1 if the layer does not exist.</summary>
    int IndexOf(string name);
}
