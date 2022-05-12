using Swordfish.Library.Types;

namespace Swordfish.Engine.Rendering
{
    public class Material
    {
        public string Name = "New Material";

        public Shader Shader = Shaders.PBR.Get();

        public Color Tint = Color.White;

        public Texture DiffuseTexture = null;
        public Texture RoughnessTexture = null;
        public Texture MetallicTexture = null;
        public Texture EmissionTexture = null;
        public Texture OcclusionTexture = null;

        public float Roughness = 0.5f;
        public float Metallic = 0.5f;
        public float Emission = 0.0f;

        public bool DoubleSided = false;
    }
}
