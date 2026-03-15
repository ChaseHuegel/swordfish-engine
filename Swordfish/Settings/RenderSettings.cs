using System.Numerics;
using Swordfish.Graphics;
using Swordfish.Library.Configuration;
using Swordfish.Library.Types;
using Tomlet.Attributes;

namespace Swordfish.Settings;

public sealed class RenderSettings : Config<RenderSettings>
{
    public DataBinding<AntiAliasing> AntiAliasing { get; private set; } = new(Settings.AntiAliasing.MSAA);
    
    public DataBinding<int> Framerate { get; private set; } = new();
    public DataBinding<bool> VSync { get; private set; } = new();

    public DataBinding<int> FOV { get; private set; } = new(90);
    public DataBinding<float> NearPlane { get; private set; } = new(0.1f);
    public DataBinding<float> FarPlane { get; private set; } = new(1000f);
    
    
    [TomlNonSerialized]
    public DataBinding<bool> Wireframe { get; private set; } = new();

    [TomlNonSerialized]
    public DataBinding<bool> HideMeshes { get; private set; } = new();
    
    [TomlNonSerialized]
    public DataBinding<TextureCubemap?> Skybox { get; private set; } = new();
    
    [TomlNonSerialized]
    public DataBinding<Vector3> AmbientLight { get; private set; } = new(new Vector3(0.5f));
}