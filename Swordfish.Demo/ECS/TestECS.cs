using System.Diagnostics;
using Ninject;
using Swordfish.ECS;
using Debugger = Swordfish.Library.Diagnostics.Debugger;

namespace Swordfish.Demo.ECS;

public static class TestECS
{
    public static void Populate()
    {
        IECSContext ecsContext = SwordfishEngine.Kernel.Get<IECSContext>();

        Stopwatch sw = Stopwatch.StartNew();
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
                    .Build();
            }
        }
        Debugger.Log("Entity creation ms: " + sw.Elapsed.TotalMilliseconds);

        sw = Stopwatch.StartNew();
        Entity[] entities = ecsContext.GetEntities();
        Debugger.Log("Pull all ms: " + sw.Elapsed.TotalMilliseconds);
        Debugger.Log("Entities: " + entities.Length);

        sw = Stopwatch.StartNew();
        Debugger.Log("DemoComponent entities: " + ecsContext.GetEntities(typeof(DemoComponent)).Length);
        Debugger.Log("Pull DemoComponent ms: " + sw.Elapsed.TotalMilliseconds);

        for (int second = 0; second < 5; second++)
        {
            sw = Stopwatch.StartNew();
            for (int frame = 0; frame < 60; frame++)
                ecsContext.Update(0f);
            Debugger.Log("Last 60 frames ms: " + sw.Elapsed.TotalMilliseconds);
        }
    }
}
