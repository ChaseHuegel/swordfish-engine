using Swordfish.Library.Diagnostics;
using Swordfish.Library.Threading;
using Swordfish.Library.Types;

namespace Swordfish.ECS;

public class ECSContext : IECSContext
{
    private const string REQ_START_MESSAGE = "The context must be started.";
    private const string REQ_STOP_MESSAGE = "The context must be stopped.";

    public DataBinding<double> Delta { get; } = new();

    public World World { get; private set; }

    private ThreadWorker ThreadWorker { get; set; }

    private bool Running;

    public ECSContext(IEntitySystem[] systems)
    {
        Debugger.Log($"Initializing ECS context.");

        Running = false;
        World = new World();

        foreach (IEntitySystem system in systems)
        {
            World.AddSystem(system);
        }

        ThreadWorker = new ThreadWorker(Update, "ECS");
    }

    public void Start()
    {
        if (Running)
            throw new InvalidOperationException(REQ_STOP_MESSAGE);

        Debugger.Log($"Starting ECS context.");
        Running = true;
        ThreadWorker.Start();
    }

    public void Stop()
    {
        Debugger.Log($"Stopping ECS context.");
        Running = false;
        ThreadWorker?.Stop();
    }

    public void Update(float delta)
    {
        if (!Running)
            throw new InvalidOperationException(REQ_START_MESSAGE);

        Delta.Set(delta);
        World.Tick(delta);
    }
}
