using System.Numerics;
using Swordfish.ECS;
using Xunit;

namespace Swordfish.Tests;

public class ChildSystemTests
{
    private sealed class ReadChildAction : IEntitySystem
    {
        public Vector3 Position;

        public void Tick(float delta, DataStore store)
        {
            var action = new CaptureAction();
            store.Query<ChildComponent, TransformComponent, CaptureAction>(delta, ref action);
            Position = action.Position;
        }

        private struct CaptureAction : IForEach<ChildComponent, TransformComponent>
        {
            public Vector3 Position;

            public void Execute(float delta, DataStore store, int entity, in ChildComponent child, in TransformComponent transform)
            {
                Position = transform.Position;
            }
        }
    }

    [Fact]
    public void ChildTransformIsCurrentForTheNextSystem()
    {
        var world = new World();
        int parent = world.DataStore.Alloc();
        world.DataStore.AddOrUpdate(parent, new TransformComponent(new Vector3(10f, 0f, 0f)));

        int child = world.DataStore.Alloc();
        world.DataStore.AddOrUpdate(child, new TransformComponent());
        world.DataStore.AddOrUpdate(child, new ChildComponent(world.DataStore.GetUuid(parent)));

        var reader = new ReadChildAction();
        world.AddSystem(new ChildSystem());
        world.AddSystem(reader);

        world.Tick(0f);

        Assert.Equal(new Vector3(10f, 0f, 0f), reader.Position);
    }
}
