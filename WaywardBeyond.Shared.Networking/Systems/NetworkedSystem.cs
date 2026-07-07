using Swordfish.ECS;
using WaywardBeyond.Shared.Networking.Components;

namespace WaywardBeyond.Shared.Networking.Systems;

public static class DataStoreExtensions
{
    public static void MarkDirty<T>(this DataStore store, int entity)
        where T : struct, IDataComponent
    {
        if (!store.TryGet(entity, out DirtyComponent dirty))
        {
            dirty = new DirtyComponent();
        }

        dirty.SetDirty<T>();
        store.AddOrUpdate(entity, dirty);
    }
}
