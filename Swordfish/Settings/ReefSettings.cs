using Swordfish.Library.Types;

namespace Swordfish.Settings;

/// <summary>Settings controlling Reef UI canvas scaling.</summary>
public sealed record ReefSettings
{
    /// <summary>UI scale multiplier; 0 selects automatic scaling from <see cref="ReferenceHeight"/>.</summary>
    public DataBinding<float> Scale { get; private set; } = new(0f);

    /// <summary>Reference window height in pixels used for automatic UI scaling.</summary>
    public DataBinding<int> ReferenceHeight { get; private set; } = new(1080);
}
