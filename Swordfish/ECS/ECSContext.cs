using Microsoft.Extensions.Logging;
using Swordfish.Library.Threading;
using Swordfish.Library.Types;

namespace Swordfish.ECS;

public class ECSContext : IECSContext
{
    private const string REQ_START_MESSAGE = "The context must be started.";
    private const string REQ_STOP_MESSAGE = "The context must be stopped.";

    public DataBinding<double> Delta { get; } = new();

    public World World { get; }

    private bool _running;
    private readonly ThreadWorker _threadWorker;
    private readonly ILogger _logger;

    public ECSContext(IEntitySystem[] systems, ILogger logger)
    {
        _logger = logger;
        _logger.LogInformation("Initializing ECS context.");

        _running = false;
        World = new World();

        foreach (IEntitySystem system in systems)
        {
            World.AddSystem(system);
        }

        _threadWorker = new ThreadWorker(Update, "ECS");
    }

    public void Start()
    {
        if (_running)
        {
            throw new InvalidOperationException(REQ_STOP_MESSAGE);
        }

        _logger.LogInformation("Starting ECS context.");
        _running = true;
        _threadWorker.Start();
    }

    public void Stop()
    {
        _logger.LogInformation("Stopping ECS context.");
        _running = false;
        _threadWorker.Stop();
    }

    public void Update(float delta)
    {
        if (!_running)
        {
            throw new InvalidOperationException(REQ_START_MESSAGE);
        }

        Delta.Set(delta);
        World.Tick(delta);
    }
}
