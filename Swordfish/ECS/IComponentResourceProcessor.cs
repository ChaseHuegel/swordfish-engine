using Krill;

namespace Swordfish.ECS;

/// <summary>Releases native resources owned by removed components.</summary>
public interface IComponentResourceProcessor
{
    /// <summary>Processes removals and pending retirements for one tick.</summary>
    void Process(DataStore store, float delta);
}
