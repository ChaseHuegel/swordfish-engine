using Swordfish.ECS;
using Swordfish.Library.Diagnostics;

using Debugger = Swordfish.Library.Diagnostics.Debugger;

namespace Swordfish.Demo.ECS;

public static class TestECS
{
    public static void Populate()
    {
        IECSContext ecsContext = SwordfishEngine.Kernel.Get<IECSContext>();

        using (Benchmark.StartNew(nameof(TestECS), nameof(Populate), "_CreateEntities"))
        {
            for (int i = 0; i < 10000; i++)
            {
                if (i % 2 == 0)
                {
                    ecsContext.EntityBuilder
                        .Attach(new IdentifierComponent($"Demo Entity {i}", null), IdentifierComponent.DefaultIndex)
                        .Attach<DemoComponent>(DemoComponent.Index)
                        .Build();
                }
                else
                {
                    ecsContext.EntityBuilder
                        .Attach(new IdentifierComponent($"Entity {i}", null), IdentifierComponent.DefaultIndex)
                        .Attach(new TransformComponent(), TransformComponent.DefaultIndex)
                        .Attach(new PhysicsComponent(), PhysicsComponent.DefaultIndex)
                        .Build();
                }
            }
        }

        using (Benchmark.StartNew(nameof(TestECS), nameof(Populate), "_GetEntities"))
            Debugger.Log("Entities: " + ecsContext.GetEntities().Length);

        using (Benchmark.StartNew(nameof(TestECS), nameof(Populate), "_GetEntities(DemoComponent)"))
            Debugger.Log("DemoComponent entities: " + ecsContext.GetEntities(typeof(DemoComponent)).Length);

        for (int second = 0; second < 5; second++)
            for (int frame = 0; frame < 60; frame++)
                ecsContext.Update(0f);
    }
}
