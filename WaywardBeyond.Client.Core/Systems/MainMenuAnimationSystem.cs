using System;
using System.Numerics;
using Swordfish.ECS;
using Swordfish.Graphics;

namespace WaywardBeyond.Client.Core.Systems;

internal sealed class MainMenuAnimationSystem : EntitySystem<TransformComponent, CameraComponent>
{
    private readonly Vector3 _axis = new(Random.Shared.NextSingle(), Random.Shared.NextSingle(), Random.Shared.NextSingle());
    
    protected override void OnTick(float delta, DataStore store, int entity, ref TransformComponent transformComponent, ref CameraComponent cameraComponent)
    {
        if (WaywardBeyond.GameState != GameState.MainMenu)
        {
            return;
        }
        
        transformComponent.Rotate(_axis * delta * 2f);
    }
}