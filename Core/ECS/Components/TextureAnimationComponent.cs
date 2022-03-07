using Swordfish.Core.Rendering;

namespace Swordfish.Core.ECS
{
    [Component]
    public struct TextureAnimationComponent
    {
        public int frames;
        public float speed;

        public float frameTime;
    }
}
