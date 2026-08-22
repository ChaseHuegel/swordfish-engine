using Swordfish.ECS;
using Xunit;

namespace Swordfish.Tests;

public class DataStoreQueryIterationTests
{
    private struct TestComp : IDataComponent
    {
        public int Value;

        public TestComp(int value) => Value = value;
    }

    private struct TestCompB : IDataComponent
    {
        public int Value;

        public TestCompB(int value) => Value = value;
    }

    [Fact]
    public void QueryVisitsLiveEntitiesAndSkipsFreed()
    {
        DataStore store = new();
        int first = store.Alloc(new TestComp(1));
        int freed = store.Alloc(new TestComp(2));
        int last = store.Alloc(new TestComp(3));
        store.Free(freed);

        int visited = 0;
        store.Query<TestComp>(0f, (float delta, DataStore s, int e, in TestComp component) => visited++);

        Assert.Equal(2, visited);
        Assert.Equal(first, 1);
        Assert.Equal(last, 3);
    }

    [Fact]
    public void QueryTwoComponentsRespectsPerStoreRanges()
    {
        DataStore store = new();
        for (var i = 0; i < 15; i++)
        {
            store.Alloc(new TestComp(i));
        }

        for (var i = 0; i < 10; i++)
        {
            store.AddOrUpdate(i + 1, new TestCompB(i));
        }

        int visited = 0;
        store.Query<TestComp, TestCompB>(0f, (float delta, DataStore s, int e, in TestComp a, in TestCompB b) => visited++);

        Assert.Equal(10, visited);
    }

    [Fact]
    public void QueryCrossesChunkBoundary()
    {
        DataStore store = new(chunkBitWidth: 4);
        for (var i = 0; i < 20; i++)
        {
            store.Alloc(new TestComp(i));
        }

        int visited = 0;
        store.Query<TestComp>(0f, (float delta, DataStore s, int e, in TestComp component) =>
        {
            visited++;
            Assert.Equal(visited, component.Value + 1);
        });

        Assert.Equal(20, visited);
    }

    [Fact]
    public void QueryEmptyChunkSkipped()
    {
        DataStore store = new();
        int a = store.Alloc(new TestComp(1));
        int b = store.Alloc(new TestComp(2));
        store.Free(a);
        store.Free(b);

        int visited = 0;
        store.Query<TestComp>(0f, (float delta, DataStore s, int e, in TestComp component) => visited++);

        Assert.Equal(0, visited);
    }

    [Fact]
    public void QueryRemovedStillDetectsWithinRange()
    {
        DataStore store = new();
        int entity = store.Alloc(new TestComp(42));
        store.ClearDirty<TestComp>(entity);
        store.Remove<TestComp>(entity);

        int value = 0;
        store.QueryRemoved<TestComp>(0f, (float delta, DataStore s, int e, in TestComp component) => value = component.Value);

        Assert.Equal(42, value);
    }

    [Fact]
    public void QueryDirtyStillDetectsWithinRange()
    {
        DataStore store = new();
        int entity = store.Alloc(new TestComp(1));
        store.ClearDirty<TestComp>(entity);

        int visited = 0;
        store.QueryDirty<TestComp>(0f, (float delta, DataStore s, int e, in TestComp component) => visited++);
        Assert.Equal(0, visited);

        store.MarkDirty<TestComp>(entity);
        store.QueryDirty<TestComp>(0f, (float delta, DataStore s, int e, in TestComp component) => visited++);
        Assert.Equal(1, visited);
    }

    [Fact]
    public void FindStillWorksOverSparseRange()
    {
        DataStore store = new();
        store.Alloc(new TestComp(1));
        store.Alloc(new TestComp(2));
        int last = store.Alloc(new TestComp(3));
        store.Free(last);

        Assert.True(store.Find((TestComp component) => component.Value == 2, out int entity));
        Assert.Equal(2, entity);
        Assert.False(store.Find((TestComp component) => component.Value == 3, out int missing));
        Assert.Equal(0, missing);
    }
}
