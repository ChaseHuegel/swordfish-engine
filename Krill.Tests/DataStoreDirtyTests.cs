using Krill;
using Xunit;

namespace Krill.Tests;

public class DataStoreDirtyTests
{
    private struct TestComponent : IDataComponent
    {
        public int Value;
    }

    private struct TestComponentB : IDataComponent;

    [Fact]
    public void AddOrUpdateMarksDirty()
    {
        DataStore store = new();
        int entity = store.Alloc();
        store.AddOrUpdate(entity, new TestComponent { Value = 1 });
        Assert.True(store.IsDirty<TestComponent>(entity));

        store.ClearDirty<TestComponent>(entity);
        Assert.False(store.IsDirty<TestComponent>(entity));
    }

    [Fact]
    public void AllocMarksDirty()
    {
        DataStore store = new();
        int entity = store.Alloc(new TestComponent { Value = 2 });
        Assert.True(store.IsDirty<TestComponent>(entity));
    }

    [Fact]
    public void QueryDirtyDoesNotClear()
    {
        DataStore store = new();
        int entity = store.Alloc();
        store.AddOrUpdate(entity, new TestComponent());

        int visited = 0;
        store.QueryDirty<TestComponent>(0f, (float delta, DataStore s, int e, in TestComponent component) => visited++);
        Assert.Equal(1, visited);
        Assert.True(store.IsDirty<TestComponent>(entity));

        store.ClearDirty<TestComponent>(entity);
        visited = 0;
        store.QueryDirty<TestComponent>(0f, (float delta, DataStore s, int e, in TestComponent component) => visited++);
        Assert.Equal(0, visited);
    }

    [Fact]
    public void QueryDirtyTwoComponentsDoesNotClear()
    {
        DataStore store = new();
        int entity = store.Alloc();
        store.AddOrUpdate(entity, new TestComponent());
        store.AddOrUpdate(entity, new TestComponentB());

        int visited = 0;
        store.QueryDirty<TestComponent, TestComponentB>(0f, (float delta, DataStore s, int e, in TestComponent component, in TestComponentB componentB) => visited++);
        Assert.Equal(1, visited);
        Assert.True(store.IsDirty<TestComponent>(entity));
        Assert.True(store.IsDirty<TestComponentB>(entity));
    }

    [Fact]
    public void QueryRemovedDetectsRemovalAndDoesNotClear()
    {
        DataStore store = new();
        int entity = store.Alloc();
        store.AddOrUpdate(entity, new TestComponent());
        store.ClearDirty<TestComponent>(entity);

        store.Remove<TestComponent>(entity);

        int visited = 0;
        store.QueryRemoved<TestComponent>(0f, (float delta, DataStore s, int e, in TestComponent component) => visited++);
        Assert.Equal(1, visited);

        visited = 0;
        store.QueryRemoved<TestComponent>(0f, (float delta, DataStore s, int e, in TestComponent component) => visited++);
        Assert.Equal(1, visited);

        store.ClearRemoved<TestComponent>(entity);
        visited = 0;
        store.QueryRemoved<TestComponent>(0f, (float delta, DataStore s, int e, in TestComponent component) => visited++);
        Assert.Equal(0, visited);
    }

    [Fact]
    public void QueryRemovedClearingDirtyDoesNotAcknowledgeRemoval()
    {
        DataStore store = new();
        int entity = store.Alloc();
        store.AddOrUpdate(entity, new TestComponent());
        store.Remove<TestComponent>(entity);

        //  A live component's dirty flag and a pending removal are separate signals, so a
        //  replication-style ClearDirty must not drop the removal.
        store.ClearDirty<TestComponent>(entity);

        int visited = 0;
        store.QueryRemoved<TestComponent>(0f, (float delta, DataStore s, int e, in TestComponent component) => visited++);
        Assert.Equal(1, visited);
    }

    [Fact]
    public void QueryRemovedSurvivesRecycledSlot()
    {
        DataStore store = new();
        int entity = store.Alloc();
        store.AddOrUpdate(entity, new TestComponent { Value = 42 });
        store.Free(entity);

        //  A freed index is reused immediately. The live value must not overwrite the removal.
        int recycled = store.Alloc();
        Assert.Equal(entity, recycled);
        store.AddOrUpdate(recycled, new TestComponent { Value = 7 });

        int value = 0;
        store.QueryRemoved<TestComponent>(0f, (float delta, DataStore s, int e, in TestComponent component) => value = component.Value);
        Assert.Equal(42, value);
    }

    [Fact]
    public void QueryRemovedSurvivesReAdd()
    {
        DataStore store = new();
        int entity = store.Alloc();
        store.AddOrUpdate(entity, new TestComponent { Value = 42 });
        store.Remove<TestComponent>(entity);
        store.AddOrUpdate(entity, new TestComponent { Value = 7 });

        int value = 0;
        store.QueryRemoved<TestComponent>(0f, (float delta, DataStore s, int e, in TestComponent component) => value = component.Value);
        Assert.Equal(42, value);

        //  Acknowledgment works even though the component exists again.
        store.ClearRemoved<TestComponent>(entity);
        int visited = 0;
        store.QueryRemoved<TestComponent>(0f, (float delta, DataStore s, int e, in TestComponent component) => visited++);
        Assert.Equal(0, visited);
        Assert.Equal(7, store.TryGet(entity, out TestComponent live) ? live.Value : -1);
    }

    [Fact]
    public void RemoveOfMissingComponentIsNotReported()
    {
        DataStore store = new();
        int entity = store.Alloc();
        store.AddOrUpdate(entity, new TestComponent());
        store.Remove<TestComponent>(entity);
        store.ClearRemoved<TestComponent>(entity);

        //  Removing an absent component keeps its old value but must not create a new removal.
        Assert.True(store.Remove<TestComponent>(entity));

        int visited = 0;
        store.QueryRemoved<TestComponent>(0f, (float delta, DataStore s, int e, in TestComponent component) => visited++);
        Assert.Equal(0, visited);
    }

    private sealed class DisposeCounter
    {
        public int Count;
    }

    private struct DisposableComponent(DisposeCounter counter) : IDataComponent, IDisposable
    {
        public readonly DisposeCounter Counter = counter;

        public void Dispose()
        {
            Counter.Count++;
        }
    }

    [Fact]
    public void DisposeRemovedDisposesRemovedDisposableComponentsOnce()
    {
        DataStore store = new();
        DisposeCounter first = new();
        DisposeCounter live = new();

        int e1 = store.Alloc(new DisposableComponent(first));
        int e2 = store.Alloc(new DisposableComponent(live));
        store.Remove<DisposableComponent>(e1);

        store.DisposeRemoved();
        store.DisposeRemoved();

        Assert.Equal(1, first.Count);
        Assert.Equal(0, live.Count);
        Assert.True(store.Has(typeof(DisposableComponent), e2));
    }

    [Fact]
    public void DisposeRemovedSurvivesRecycledSlot()
    {
        DataStore store = new();
        DisposeCounter first = new();
        DisposeCounter second = new();

        int entity = store.Alloc(new DisposableComponent(first));
        store.Free(entity);

        int recycled = store.Alloc(new DisposableComponent(second));
        Assert.Equal(entity, recycled);

        store.DisposeRemoved();

        Assert.Equal(1, first.Count);
        Assert.Equal(0, second.Count);
    }

    [Fact]
    public void QueryRemovedPreservesLastValue()
    {
        DataStore store = new();
        int entity = store.Alloc();
        store.AddOrUpdate(entity, new TestComponent { Value = 42 });
        store.ClearDirty<TestComponent>(entity);

        store.Remove<TestComponent>(entity);

        int value = 0;
        store.QueryRemoved<TestComponent>(0f, (float delta, DataStore s, int e, in TestComponent component) =>
        {
            value = component.Value;
        });

        Assert.Equal(42, value);
    }

    [Fact]
    public void QueryRefWriteMarksDirty()
    {
        DataStore store = new();
        int entity = store.Alloc();
        store.AddOrUpdate(entity, new TestComponent());
        store.ClearDirty<TestComponent>(entity);

        store.QueryRef<TestComponent>(0f, (float delta, DataStore s, int e, ref Ref<TestComponent> component) =>
        {
            component.Write.Value = 7;
        });

        Assert.True(store.IsDirty<TestComponent>(entity));
    }

    [Fact]
    public void QueryRefReadDoesNotMarkDirty()
    {
        DataStore store = new();
        int entity = store.Alloc();
        store.AddOrUpdate(entity, new TestComponent());
        store.ClearDirty<TestComponent>(entity);

        store.QueryRef<TestComponent>(0f, (float delta, DataStore s, int e, ref Ref<TestComponent> component) =>
        {
            int value = component.Read.Value;
        });

        Assert.False(store.IsDirty<TestComponent>(entity));
    }

    [Fact]
    public void QueryRefSingleEntityMarksDirty()
    {
        DataStore store = new();
        int entity = store.Alloc();
        store.AddOrUpdate(entity, new TestComponent());
        store.ClearDirty<TestComponent>(entity);

        store.QueryRef<TestComponent>(entity, 0f, (float delta, DataStore s, int e, ref Ref<TestComponent> component) =>
        {
            component.Write.Value = 9;
        });

        Assert.True(store.IsDirty<TestComponent>(entity));
    }

    [Fact]
    public void QueryRefTwoComponentsMarksOnlyWritten()
    {
        DataStore store = new();
        int entity = store.Alloc();
        store.AddOrUpdate(entity, new TestComponent());
        store.AddOrUpdate(entity, new TestComponentB());
        store.ClearDirty<TestComponent>(entity);
        store.ClearDirty<TestComponentB>(entity);

        store.QueryRef<TestComponent, TestComponentB>(0f, (float delta, DataStore s, int e, ref Ref<TestComponent> component, ref Ref<TestComponentB> componentB) =>
        {
            component.Write.Value = 1;
        });

        Assert.True(store.IsDirty<TestComponent>(entity));
        Assert.False(store.IsDirty<TestComponentB>(entity));
    }

    [Fact]
    public void RemoveClearsExistsPreservesValueAndMarksDirty()
    {
        DataStore store = new();
        int entity = store.Alloc();
        store.AddOrUpdate(entity, new TestComponent { Value = 5 });
        store.ClearDirty<TestComponent>(entity);

        Assert.True(store.Remove<TestComponent>(entity));
        Assert.True(store.IsDirty<TestComponent>(entity), "Removal must flow through the dirty poll.");
        Assert.Equal(0, CountTestComponents(store));

        Assert.True(store.Remove(typeof(TestComponent), entity), "Removing again reports absence.");
    }

    [Fact]
    public void HasProbesRuntimeTypeExistence()
    {
        DataStore store = new();
        int entity = store.Alloc();
        store.AddOrUpdate(entity, new TestComponent());

        Assert.True(store.Has(typeof(TestComponent), entity));
        Assert.False(store.Has(typeof(TestComponentB), entity));

        store.Remove<TestComponent>(entity);
        Assert.False(store.Has(typeof(TestComponent), entity), "Removal clears existence.");
    }

    [Fact]
    public void RemoveUnknownTypeAndBareEntitySurvives()
    {
        DataStore store = new();
        int entity = store.Alloc();
        store.AddOrUpdate(entity, new TestComponent());

        Assert.False(store.Remove(typeof(string), entity));

        store.Remove<TestComponent>(entity);

        //  The entity survives removal as a bare entity.
        int visits = 0;
        store.Query(0f, (float _, DataStore s, int e) => { if (s.TryGet(e, out TestComponent _)) visits++; });
        Assert.Equal(0, visits);

        //  ...but the slot itself is still alive (queries see it).
        store.AddOrUpdate(entity, new TestComponent { Value = 1 });
        Assert.Equal(1, CountTestComponents(store));
    }

    private static int CountTestComponents(DataStore store)
    {
        int count = 0;
        store.Query<TestComponent>(0f, (float _, DataStore s, int e, in TestComponent component) => count++);
        return count;
    }
}
