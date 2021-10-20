using Swordfish.Rendering;

namespace Swordfish.ECS
{
    [Component]
    public struct TextureAnimationComponent
    {
        public int frames;
        public float speed;

        public float frameTime;
    }
}
