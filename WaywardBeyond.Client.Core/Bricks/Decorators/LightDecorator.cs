using System.Numerics;
using Swordfish.Bricks;
using Swordfish.ECS;
using WaywardBeyond.Client.Core.Components;

namespace WaywardBeyond.Client.Core.Bricks.Decorators;

internal sealed class LightDecorator : IBrickDecorator
{
    public void OnBrickAdded(DataStore store, int entity, BrickGrid grid, int x, int y, int z, Brick brick, BrickInfo info)
    {
        if (!info.LightSource)
        {
            return;
        }
        
        int lightEntity = store.Alloc();
        store.AddOrUpdate(lightEntity, new IdentifierComponent(name: null, tag: "game"));
        store.AddOrUpdate(lightEntity, new TransformComponent());
        store.AddOrUpdate(lightEntity, new BrickIdentifierComponent(x, y, z));
        store.AddOrUpdate(lightEntity, new LightComponent(radius: info.Brightness, color: new Vector3(0.25f), size: 2.5f));
        store.AddOrUpdate(lightEntity, new ChildComponent(entity)
        {
            LocalPosition = new Vector3(x, y, z),
        });
    }

    public void OnBrickRemoved(DataStore store, int entity, BrickGrid grid, int x, int y, int z, Brick brick, BrickInfo info)
    {
        if (!info.LightSource)
        {
            return;
        }
        
        store.Query<BrickIdentifierComponent, LightComponent>(0f, LightQuery);
        void LightQuery(float delta, DataStore _, int lightEntity, ref BrickIdentifierComponent brickIdentifier, ref LightComponent light)
        {
            if (brickIdentifier.X != x || brickIdentifier.Y != y || brickIdentifier.Z != z)
            {
                return;
            }
            
            if (!store.TryGet(lightEntity, out ChildComponent childComponent))
            {
                return;
            }
            
            if (childComponent.Parent != entity) 
            {
                return;
            }
            
            store.Free(lightEntity);
        }
    }
}