using Swordfish.Library.Threading;

namespace Shoal.Modularity;

/// <summary>
///     A long-running object that executes on a dedicated thread upon the engine starting.
/// </summary>
// ReSharper disable once UnusedType.Global
public abstract class ThreadedEntryPoint : IEntryPoint
{
    protected virtual int TickRate => 64;

    public readonly ThreadContext Context = ThreadContext.FromCurrentThread();
    
    public void Run()
    {
        var threadWorker = new ThreadWorker(Tick, GetType().FullName)
        {
            TargetTickRate = TickRate,
        };

        threadWorker.Start();
    }

    private void Tick(float delta)
    {
        Context.SwitchToCurrentThread();
        Context.ProcessMessageQueue();
        OnTick(delta);
    }
    
    protected abstract void OnTick(float delta);
}