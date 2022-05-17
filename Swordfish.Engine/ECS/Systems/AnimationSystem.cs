namespace Swordfish.Engine.ECS
{
    [ComponentSystem(typeof(RenderComponent), typeof(TextureAnimationComponent))]
    public class AnimationSystem : ComponentSystem
    {
        public override void OnUpdateEntity(float deltaTime, Entity entity)
        {
            TextureAnimationComponent animator = Swordfish.ECS.Get<TextureAnimationComponent>(entity);

            Swordfish.ECS.Do<RenderComponent>(entity, x =>
            {
                if (animator.frameTime >= animator.speed/ animator.frames)
                {
                    if (x.mesh.uvOffset.Y <= 0f)
                        x.mesh.uvOffset.Y = 1f;
                    else
                        x.mesh.uvOffset.Y -= 1f / animator.frames;
                }

                return x;
            });

            Swordfish.ECS.Do<TextureAnimationComponent>(entity, x =>
            {
                if (x.frameTime >= animator.speed/ animator.frames)
                    x.frameTime -= animator.speed/ animator.frames;

                x.frameTime += deltaTime;

                return x;
            });
        }
    }
}