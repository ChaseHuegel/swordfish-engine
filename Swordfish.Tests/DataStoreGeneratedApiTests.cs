using Swordfish.ECS;
using Xunit;

namespace Swordfish.Tests;

public class DataStoreGeneratedApiTests
{
    private struct TestCompA : IDataComponent
    {
        public int Value;

        public TestCompA(int value) => Value = value;
    }

    private struct TestCompB : IDataComponent
    {
        public float Value;

        public TestCompB(float value) => Value = value;
    }

    private struct TestCompC : IDataComponent
    {
        public string? Value;

        public TestCompC(string? value) => Value = value;
    }

    private struct TestCompD : IDataComponent
    {
        public long Value;

        public TestCompD(long value) => Value = value;
    }

    private struct TestCompMissing : IDataComponent;

    private sealed class TickSystemThree : EntitySystem<TestCompA, TestCompB, TestCompC>
    {
        public int Visited;

        protected override void OnTick(float delta, DataStore store, int entity, in TestCompA component1, in TestCompB component2, in TestCompC component3)
        {
            Visited++;
        }
    }

    private sealed class RefSystemThree : EntitySystemRef<TestCompA, TestCompB, TestCompC>
    {
        protected override void OnTick(float delta, DataStore store, int entity, ref Ref<TestCompA> component1, ref Ref<TestCompB> component2, ref Ref<TestCompC> component3)
        {
            component1.Write.Value = 42;
        }
    }

    [Fact]
    public void AllocFourComponentsQueryReturnsValues()
    {
        DataStore store = new();
        store.Alloc(new TestCompA(1), new TestCompB(2f), new TestCompC("three"), new TestCompD(4L));

        int visited = 0;
        store.Query<TestCompA, TestCompB, TestCompC, TestCompD>(0f,
            (float delta, DataStore s, int e, in TestCompA a, in TestCompB b, in TestCompC c, in TestCompD d) =>
            {
                visited++;
                Assert.Equal(1, a.Value);
                Assert.Equal(2f, b.Value);
                Assert.Equal("three", c.Value);
                Assert.Equal(4L, d.Value);
            });

        Assert.Equal(1, visited);
    }

    [Fact]
    public void AddOrUpdateFourComponentsRoundTrip()
    {
        DataStore store = new();
        int entity = store.Alloc();

        store.AddOrUpdate(entity, new TestCompA(1), new TestCompB(2f), new TestCompC("three"), new TestCompD(4L));

        Assert.True(store.TryGet(entity, out TestCompA a, out TestCompB b, out TestCompC c, out TestCompD d));
        Assert.Equal("three", c.Value);
        Assert.Equal(4L, d.Value);
    }

    [Fact]
    public void RemoveThreeComponentsReturnsTrueAndRemoves()
    {
        DataStore store = new();
        int entity = store.Alloc(new TestCompA(1), new TestCompB(2f), new TestCompC("three"));

        Assert.True(store.Remove<TestCompA, TestCompB, TestCompC>(entity));
        Assert.False(store.TryGet(entity, out TestCompA a));
    }

    [Fact]
    public void QueryRemovedTwoComponentsFiresOnlyWhenAllRemoved()
    {
        DataStore store = new();
        int entity = store.Alloc(new TestCompA(1), new TestCompB(2f));
        store.ClearDirty<TestCompA>(entity);
        store.ClearDirty<TestCompB>(entity);

        //  Partial removal must NOT fire the AND-removed query
        store.Remove<TestCompA>(entity);
        int visited = 0;
        store.QueryRemoved<TestCompA, TestCompB>(0f,
            (float delta, DataStore s, int e, in TestCompA a, in TestCompB b) => visited++);
        Assert.Equal(0, visited);

        //  Full removal fires
        store.Remove<TestCompB>(entity);
        store.QueryRemoved<TestCompA, TestCompB>(0f,
            (float delta, DataStore s, int e, in TestCompA a, in TestCompB b) => visited++);
        Assert.Equal(1, visited);
    }

    [Fact]
    public void QueryRemovedThreeComponentsAndSemantics()
    {
        DataStore store = new();
        int entity = store.Alloc(new TestCompA(1), new TestCompB(2f), new TestCompC("three"));
        store.ClearDirty<TestCompA>(entity);
        store.ClearDirty<TestCompB>(entity);
        store.ClearDirty<TestCompC>(entity);

        store.Remove<TestCompA>(entity);
        int visited = 0;
        store.QueryRemoved<TestCompA, TestCompB, TestCompC>(0f,
            (float delta, DataStore s, int e, in TestCompA a, in TestCompB b, in TestCompC c) => visited++);
        Assert.Equal(0, visited);

        store.Remove<TestCompB, TestCompC>(entity);
        store.QueryRemoved<TestCompA, TestCompB, TestCompC>(0f,
            (float delta, DataStore s, int e, in TestCompA a, in TestCompB b, in TestCompC c) => visited++);
        Assert.Equal(1, visited);
    }

    [Fact]
    public void QueryRefThreeComponentsWriteMarksDirty()
    {
        DataStore store = new();
        int entity = store.Alloc(new TestCompA(1), new TestCompB(2f), new TestCompC("three"));
        store.ClearDirty<TestCompA>(entity);
        store.ClearDirty<TestCompB>(entity);
        store.ClearDirty<TestCompC>(entity);

        store.QueryRef<TestCompA, TestCompB, TestCompC>(0f,
            (float delta, DataStore s, int e, ref Ref<TestCompA> a, ref Ref<TestCompB> b, ref Ref<TestCompC> c) =>
            {
                c.Write.Value = "written";
            });

        Assert.False(store.IsDirty<TestCompA>(entity));
        Assert.False(store.IsDirty<TestCompB>(entity));
        Assert.True(store.IsDirty<TestCompC>(entity));
    }

    [Fact]
    public void QueryRefDirtyTwoComponentsVisitsOnlyDirty()
    {
        DataStore store = new();
        int dirtyEntity = store.Alloc(new TestCompA(1), new TestCompB(2f));
        int cleanEntity = store.Alloc(new TestCompA(3), new TestCompB(4f));
        store.ClearDirty<TestCompA>(dirtyEntity);
        store.ClearDirty<TestCompB>(dirtyEntity);
        store.ClearDirty<TestCompA>(cleanEntity);
        store.ClearDirty<TestCompB>(cleanEntity);

        store.MarkDirty<TestCompA>(dirtyEntity);

        int visited = 0;
        store.QueryRefDirty<TestCompA, TestCompB>(0f,
            (float delta, DataStore s, int e, ref Ref<TestCompA> a, ref Ref<TestCompB> b) =>
            {
                visited++;
                Assert.Equal(dirtyEntity, e);
                a.Write.Value = 99;
            });

        Assert.Equal(1, visited);
        Assert.True(store.IsDirty<TestCompA>(dirtyEntity));
        Assert.False(store.IsDirty<TestCompB>(cleanEntity));
    }

    [Fact]
    public void QueryDirtyThreeComponentsFiresWhenAnyDirty()
    {
        DataStore store = new();
        int entity = store.Alloc(new TestCompA(1), new TestCompB(2f), new TestCompC("three"));
        store.ClearDirty<TestCompA>(entity);
        store.ClearDirty<TestCompB>(entity);
        store.ClearDirty<TestCompC>(entity);

        store.MarkDirty<TestCompB>(entity);

        int visited = 0;
        store.QueryDirty<TestCompA, TestCompB, TestCompC>(0f,
            (float delta, DataStore s, int e, in TestCompA a, in TestCompB b, in TestCompC c) => visited++);
        Assert.Equal(1, visited);
    }

    [Fact]
    public void SingleEntityQueryThreeComponents()
    {
        DataStore store = new();
        int entity = store.Alloc(new TestCompA(1), new TestCompB(2f), new TestCompC("three"));

        int visited = 0;
        store.Query<TestCompA, TestCompB, TestCompC>(entity, 0f,
            (float delta, DataStore s, int e, in TestCompA a, in TestCompB b, in TestCompC c) =>
            {
                visited++;
                Assert.Equal(entity, e);
                Assert.Equal("three", c.Value);
            });

        Assert.Equal(1, visited);
    }

    [Fact]
    public void SingleEntityQueryRefThreeComponentsMarksDirty()
    {
        DataStore store = new();
        int entity = store.Alloc(new TestCompA(1), new TestCompB(2f), new TestCompC("three"));
        store.ClearDirty<TestCompA>(entity);
        store.ClearDirty<TestCompB>(entity);
        store.ClearDirty<TestCompC>(entity);

        store.QueryRef<TestCompA, TestCompB, TestCompC>(entity, 0f,
            (float delta, DataStore s, int e, ref Ref<TestCompA> a, ref Ref<TestCompB> b, ref Ref<TestCompC> c) =>
            {
                b.Write.Value = 7f;
            });

        Assert.False(store.IsDirty<TestCompA>(entity));
        Assert.True(store.IsDirty<TestCompB>(entity));
        Assert.False(store.IsDirty<TestCompC>(entity));
    }

    [Fact]
    public void TryGetFourComponents()
    {
        DataStore store = new();
        int entity = store.Alloc(new TestCompA(1), new TestCompB(2f), new TestCompC("three"), new TestCompD(4L));

        Assert.True(store.TryGet(entity, out TestCompA a, out TestCompB b, out TestCompC c, out TestCompD d));
        Assert.Equal(4L, d.Value);

        Assert.False(store.TryGet(entity, out TestCompA a2, out TestCompB b2, out TestCompC c2, out TestCompMissing missing));
    }

    [Fact]
    public void EntityAddOrUpdateRemoveFourComponents()
    {
        DataStore store = new();
        Entity entity = new(store.Alloc(), store);

        Assert.False(entity.Has<TestCompA, TestCompB, TestCompC>());
        entity.AddOrUpdate(new TestCompA(1), new TestCompB(2f), new TestCompC("three"), new TestCompD(4L));
        Assert.True(entity.Has<TestCompA, TestCompB, TestCompC, TestCompD>());

        Assert.True(entity.TryGet(out TestCompA a, out TestCompB b, out TestCompC c, out TestCompD d));
        Assert.Equal(1, a.Value);

        Assert.True(entity.Remove<TestCompA, TestCompB>());
        Assert.False(entity.Has<TestCompA>());
        Assert.True(entity.Has<TestCompC>());
    }

    [Fact]
    public void EntityAddAddsAllComponents()
    {
        DataStore store = new();
        Entity entity = new(store.Alloc(), store);

        Assert.True(entity.Add<TestCompA, TestCompB>());
        Assert.False(entity.Add<TestCompA, TestCompB>());
        Assert.True(entity.Has<TestCompA, TestCompB>());
    }

    [Fact]
    public void EntitySystemThreeComponentsTicks()
    {
        DataStore store = new();
        store.Alloc(new TestCompA(1), new TestCompB(2f), new TestCompC("x"));

        TickSystemThree system = new();
        system.Tick(0f, store);

        Assert.Equal(1, system.Visited);
    }

    [Fact]
    public void EntitySystemRefThreeComponentsMarksDirty()
    {
        DataStore store = new();
        int entity = store.Alloc(new TestCompA(1), new TestCompB(2f), new TestCompC("x"));
        store.ClearDirty<TestCompA>(entity);
        store.ClearDirty<TestCompB>(entity);
        store.ClearDirty<TestCompC>(entity);

        RefSystemThree system = new();
        system.Tick(0f, store);

        Assert.True(store.IsDirty<TestCompA>(entity));
        Assert.False(store.IsDirty<TestCompB>(entity));
    }

    [Fact]
    public void FreedEntityIdIsRecycled()
    {
        DataStore store = new();
        int first = store.Alloc();
        int second = store.Alloc();
        store.Free(first);

        int third = store.Alloc();

        Assert.Equal(first, third);
        Assert.NotEqual(second, third);
    }

    [Fact]
    public void QuerySkipsRecycledEntities()
    {
        DataStore store = new();
        int entity = store.Alloc();
        store.Free(entity);

        int visited = 0;
        store.Query(0f, (float delta, DataStore s, int e) => visited++);

        Assert.Equal(0, visited);
    }
}
