using Microsoft.Extensions.Logging;
using Shoal.Modularity;
using Swordfish.Graphics;
using Swordfish.Library.Configuration;
using Swordfish.Library.Threading;
using Swordfish.Library.Types;
using Swordfish.Settings;

namespace Swordfish.ECS;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class ECSContext : IECSContext, IDisposable, IEntryPoint
{
    public DataBinding<double> Delta { get; } = new();
    public World World { get; }

    private readonly ThreadWorker _threadWorker;
    private readonly ILogger _logger;

    public ECSContext(IEntitySystem[] systems, ILogger logger, RenderSettings renderSettings)
    {
        _logger = logger;

        _threadWorker = new ThreadWorker(Update, "ECS")
        {
            TargetTickRate = -1,
        };

        World = new World();
        foreach (IEntitySystem system in systems)
        {
            World.AddSystem(system);
        }
        
        //  Ensure at least one Camera exists
        Entity camera = World.NewEntity();
        var viewFrustum = new ViewFrustumComponent(renderSettings.NearPlane, renderSettings.FarPlane, renderSettings.FOV);
        camera.AddOrUpdate(viewFrustum);
        camera.Add<CameraComponent>();
        camera.Add<TransformComponent>();
        
        _logger.LogInformation("Initialized ECS.");
    }

    public void Run()
    {
        _threadWorker.Start();
        _logger.LogInformation("Started ECS thread.");
    }

    public void Dispose()
    {
        _threadWorker.Stop();
        _logger.LogInformation("Stopped ECS thread.");
    }
    
    private void Update(float delta)
    {
        Delta.Set(delta);
        World.Tick(delta);
    }
}
