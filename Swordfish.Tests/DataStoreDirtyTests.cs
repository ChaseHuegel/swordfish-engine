using Swordfish.ECS;
using Xunit;

namespace Swordfish.Tests;

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

        store.ClearDirty<TestComponent>(entity);
        visited = 0;
        store.QueryRemoved<TestComponent>(0f, (float delta, DataStore s, int e, in TestComponent component) => visited++);
        Assert.Equal(0, visited);
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
}
