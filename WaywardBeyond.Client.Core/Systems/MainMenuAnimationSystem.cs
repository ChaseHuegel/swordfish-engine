using System;
using System.Numerics;
using Swordfish.ECS;
using Swordfish.Graphics;

namespace WaywardBeyond.Client.Core.Systems;

internal sealed class MainMenuAnimationSystem : IEntitySystem
{
    private readonly Vector3 _axis = new(Random.Shared.NextSingle(), Random.Shared.NextSingle(), Random.Shared.NextSingle());

    private struct ForEachAction : IForEachRef<TransformComponent, CameraComponent>
    {
        public MainMenuAnimationSystem Owner;

        public void Execute(float delta, DataStore store, int entity, ref Ref<TransformComponent> transformComponent, ref Ref<CameraComponent> cameraComponent)
        {
            if (WaywardBeyond.GameState != GameState.MainMenu)
            {
                return;
            }

            transformComponent.Write.Rotate(Owner._axis * delta);
        }
    }

    public void Tick(float delta, DataStore store)
    {
        ForEachAction action = new() { Owner = this };
        store.QueryRef<TransformComponent, CameraComponent, ForEachAction>(delta, ref action);
    }
}