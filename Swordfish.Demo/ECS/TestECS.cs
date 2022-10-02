using System.Diagnostics;
using Swordfish.ECS;
using Debugger = Swordfish.Library.Diagnostics.Debugger;

namespace Swordfish.Demo.ECS;

public static class TestECS
{
    public static void Initialize()
    {
        var world = new World();
        world.BindSystem<DemoSystem>();
        DemoSystem.ComponentIndex = world.BindComponent<DemoComponent>();

        Stopwatch sw = Stopwatch.StartNew();
        world.Initialize();
        Debugger.Log("World start ms: " + sw.Elapsed.TotalMilliseconds);

        sw = Stopwatch.StartNew();
        for (int i = 0; i < 10000; i++)
        {
            if (i % 2 == 0)
                world.EntityBuilder.Attach<DemoComponent>(DemoSystem.ComponentIndex).Build();
            else
                world.EntityBuilder.Build();
        }
        Debugger.Log("Entity creation ms: " + sw.Elapsed.TotalMilliseconds);

        sw = Stopwatch.StartNew();
        Entity[] entities = world.GetEntities();
        Debugger.Log("Pull all ms: " + sw.Elapsed.TotalMilliseconds);
        Debugger.Log("Entities: " + entities.Length);

        sw = Stopwatch.StartNew();
        Debugger.Log("DemoComponent entities: " + world.GetEntities(typeof(DemoComponent)).Length);
        Debugger.Log("Pull DemoComponent ms: " + sw.Elapsed.TotalMilliseconds);

        for (int second = 0; second < 5; second++)
        {
            sw = Stopwatch.StartNew();
            for (int frame = 0; frame < 60; frame++)
                world.Update(0f);
            Debugger.Log("Last 60 frames ms: " + sw.Elapsed.TotalMilliseconds);
        }

        Debugger.Log("DemoComponent value: " + entities[0].GetComponent<DemoComponent>()?.Value);

        sw = Stopwatch.StartNew();
        for (int i = 0; i < entities.Length / 2; i++)
            world.RemoveEntity(entities[i]);
        Debugger.Log("Remove entities ms: " + sw.Elapsed.TotalMilliseconds);
        Debugger.Log("Entities: " + world.GetEntities().Length);
    }
}
