using System.Drawing;
using System.Linq;
using System.Numerics;
using Swordfish.Bricks;
using Swordfish.ECS;
using Swordfish.Library.Extensions;
using WaywardBeyond.Client.Core.Components;

namespace WaywardBeyond.Client.Core.Bricks.Decorators;

internal sealed class ThrusterDecorator : IBrickDecorator
{
    private static readonly Vector3 _lightColor = Color.FromArgb(244, 126, 27).ToVector3() * 20;

    public void OnBrickAdded(DataStore store, int entity, BrickGrid grid, int x, int y, int z, Brick brick, BrickInfo info)
    {
        if (!info.Tags.Contains("thruster"))
        {
            return;
        }
        
        int lightEntity = store.Alloc();
        store.AddOrUpdate(lightEntity, new TransformComponent());
        store.AddOrUpdate(lightEntity, new BrickIdentifierComponent(x, y, z));
        store.AddOrUpdate(lightEntity, new LightComponent(radius: 0.75f, color: _lightColor, size: 0.25f));
        store.AddOrUpdate(lightEntity, new ChildComponent(entity)
        {
            LocalPosition = new Vector3(x, y, z),
        });
    }

    public void OnBrickRemoved(DataStore store, int entity, BrickGrid grid, int x, int y, int z, Brick brick, BrickInfo info)
    {
        if (!info.Tags.Contains("thruster"))
        {
            return;
        }
        
        store.Query<BrickIdentifierComponent, LightComponent>(0f, ThrusterQuery);
        void ThrusterQuery(float delta, DataStore _, int lightEntity, ref BrickIdentifierComponent brickIdentifier, ref LightComponent light)
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