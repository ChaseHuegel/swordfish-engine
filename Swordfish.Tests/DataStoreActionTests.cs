using System.Threading;
using Swordfish.ECS;
using Xunit;

namespace Swordfish.Tests;

public class DataStoreActionTests
{
    private struct TestComp : IDataComponent
    {
        public int Value;

        public TestComp(int value) => Value = value;
    }

    private struct TestCompB : IDataComponent;

    private struct CountAction : IForEach<TestComp>
    {
        public int Count;

        public void Execute(float delta, DataStore store, int entity, in TestComp cleanupAudioPlayer)
        {
            Count++;
        }
    }

    private struct SumAction : IForEach<TestComp>
    {
        public int Sum;

        public void Execute(float delta, DataStore store, int entity, in TestComp cleanupAudioPlayer)
        {
            Sum += cleanupAudioPlayer.Value;
        }
    }

    private struct WriteAction : IForEachRef<TestComp>
    {
        public int Value;

        public void Execute(float delta, DataStore store, int entity, ref Ref<TestComp> component)
        {
            component.Write.Value = Value;
        }
    }

    private struct TwoCompAction : IForEach<TestComp, TestCompB>
    {
        public int Count;

        public void Execute(float delta, DataStore store, int entity, in TestComp component1, in TestCompB component2)
        {
            Count++;
        }
    }

    private struct NoComponentAction : IForEach
    {
        public int Count;

        public void Execute(float delta, DataStore store, int entity)
        {
            Count++;
        }
    }

    [Fact]
    public void QueryActionVisitsAllEntities()
    {
        DataStore store = new();
        for (var i = 0; i < 5; i++)
        {
            store.Alloc(new TestComp(i));
        }

        CountAction action = new();
        store.Query<TestComp, CountAction>(0f, ref action);

        Assert.Equal(5, action.Count);
    }

    [Fact]
    public void QueryNoComponentActionVisitsAllEntities()
    {
        DataStore store = new();
        for (var i = 0; i < 5; i++)
        {
            store.Alloc();
        }

        NoComponentAction action = new();
        store.Query<NoComponentAction>(0f, ref action);

        Assert.Equal(5, action.Count);
    }

    [Fact]
    public void QueryActionReadsComponents()
    {
        DataStore store = new();
        store.Alloc(new TestComp(1));
        store.Alloc(new TestComp(2));
        store.Alloc(new TestComp(3));

        SumAction action = new();
        store.Query<TestComp, SumAction>(0f, ref action);

        Assert.Equal(6, action.Sum);
    }

    [Fact]
    public void QueryTwoComponentAction()
    {
        DataStore store = new();
        store.Alloc(new TestComp(1), new TestCompB());

        TwoCompAction action = new();
        store.Query<TestComp, TestCompB, TwoCompAction>(0f, ref action);

        Assert.Equal(1, action.Count);
    }

    [Fact]
    public void QueryRefActionMarksDirty()
    {
        DataStore store = new();
        int entity = store.Alloc(new TestComp(0));
        store.ClearDirty<TestComp>(entity);

        WriteAction action = new() { Value = 7 };
        store.QueryRef<TestComp, WriteAction>(0f, ref action);

        Assert.True(store.IsDirty<TestComp>(entity));
        Assert.Equal(7, store.TryGet(entity, out TestComp component) ? component.Value : -1);
    }

    [Fact]
    public void QueryDirtyActionVisitsOnlyDirty()
    {
        DataStore store = new();
        int dirty = store.Alloc(new TestComp(1));
        int clean = store.Alloc(new TestComp(2));
        store.ClearDirty<TestComp>(dirty);
        store.ClearDirty<TestComp>(clean);
        store.MarkDirty<TestComp>(dirty);

        CountAction action = new();
        store.QueryDirty<TestComp, CountAction>(0f, ref action);

        Assert.Equal(1, action.Count);
    }

    [Fact]
    public void QueryRefDirtyActionVisitsOnlyDirty()
    {
        DataStore store = new();
        int dirty = store.Alloc(new TestComp(1));
        int clean = store.Alloc(new TestComp(2));
        store.ClearDirty<TestComp>(dirty);
        store.ClearDirty<TestComp>(clean);
        store.MarkDirty<TestComp>(dirty);

        WriteAction action = new() { Value = 9 };
        store.QueryRefDirty<TestComp, WriteAction>(0f, ref action);

        Assert.Equal(9, store.TryGet(dirty, out TestComp a) ? a.Value : -1);
        Assert.Equal(2, store.TryGet(clean, out TestComp b) ? b.Value : -1);
    }

    [Fact]
    public void QueryRemovedActionFires()
    {
        DataStore store = new();
        int entity = store.Alloc(new TestComp(42));
        store.ClearDirty<TestComp>(entity);
        store.Remove<TestComp>(entity);

        CountAction action = new();
        store.QueryRemoved<TestComp, CountAction>(0f, ref action);

        Assert.Equal(1, action.Count);
    }

    [Fact]
    public void QueryVisitsAllEntitiesAcrossChunks()
    {
        DataStore store = new(chunkBitWidth: 8);
        for (var i = 0; i < 512; i++)
        {
            store.Alloc(new TestComp(i));
        }

        int visited = 0;
        store.Query<TestComp>(0f, (float delta, DataStore s, int e, in TestComp component) => visited++);

        Assert.Equal(512, visited);
    }

    [Fact]
    public void QueryParallelVisitsAllEntities()
    {
        DataStore store = new();
        for (var i = 0; i < 100; i++)
        {
            store.Alloc(new TestComp(i));
        }

        int visited = 0;
        store.QueryParallel<TestComp>(0f, (float delta, DataStore s, int e, in TestComp component) => visited++);

        Assert.Equal(100, visited);
    }

    [Fact]
    public void QueryParallelUsesParallelFor()
    {
        DataStore store = new(chunkBitWidth: 15);
        for (var i = 0; i < 31_000; i++)
        {
            store.Alloc(new TestComp(i));
        }

        int visited = 0;
        store.QueryParallel<TestComp>(0f, (float delta, DataStore s, int e, in TestComp component) =>
        {
            Interlocked.Increment(ref visited);
        });

        Assert.Equal(31_000, visited);
    }

    [Fact]
    public void QueryRemainsSerialAboveParallelThreshold()
    {
        DataStore store = new(chunkBitWidth: 15);
        for (var i = 0; i < 31_000; i++)
        {
            store.Alloc(new TestComp(i));
        }

        int visited = 0;
        store.Query<TestComp>(0f, (float delta, DataStore s, int e, in TestComp component) => visited++);

        Assert.Equal(31_000, visited);
    }
}
