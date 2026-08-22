using BenchmarkDotNet.Attributes;
using Swordfish.ECS;

namespace Swordfish.ECS.Benchmarks;

[MemoryDiagnoser]
public class QueryBenchmarks
{
    private const int SparseCount = 100;
    private const int DenseCount = 50_000;
    private const int MixedWideCount = 1_000;
    private const int MixedNarrowCount = 50;

    private readonly DataStore _sparseStore = CreateSparse();
    private readonly DataStore _denseStore = CreateDense();
    private readonly DataStore _mixedStore = CreateMixed();

    private readonly ForEach<BenchA> _queryA = static (float _, DataStore _, int _, in BenchA a) => _ = a.Value;
    private readonly ForEach<BenchA, BenchB> _queryAB = static (float _, DataStore _, int _, in BenchA a, in BenchB b) => _ = a.Value + b.Value;
    private readonly ForEachRef<BenchA> _refA = static (float _, DataStore _, int _, ref Ref<BenchA> a) => a.Write.Value = 1;
    private readonly Predicate<BenchA> _findA = static (BenchA a) => a.Value == int.MaxValue;

    private NoopAction _noopAction = new();
    private RefAction _refAction = new();

    [Benchmark]
    public void Query_SparseSingleComponent() => _sparseStore.Query<BenchA>(0f, _queryA);

    [Benchmark]
    public void Query_SparseTwoComponents() => _sparseStore.Query<BenchA, BenchB>(0f, _queryAB);

    [Benchmark]
    public void Query_DenseSingleComponent() => _denseStore.Query<BenchA>(0f, _queryA);

    [Benchmark]
    public void QueryParallel_DenseSingleComponent() => _denseStore.QueryParallel<BenchA>(0f, _queryA);

    [Benchmark]
    public void Query_MixedRangesTwoComponents() => _mixedStore.Query<BenchA, BenchB>(0f, _queryAB);

    [Benchmark]
    public void QueryRef_SparseSingleComponent() => _sparseStore.QueryRef<BenchA>(0f, _refA);

    [Benchmark]
    public void QueryDirty_SparseSingleComponent() => _sparseStore.QueryDirty<BenchA>(0f, _queryA);

    [Benchmark]
    public void Find_SparseSingleComponent() => _sparseStore.Find(_findA, out _);

    [Benchmark]
    public void QueryAction_SparseSingleComponent() => _sparseStore.Query<BenchA, NoopAction>(0f, ref _noopAction);

    [Benchmark]
    public void QueryAction_DenseSingleComponent() => _denseStore.Query<BenchA, NoopAction>(0f, ref _noopAction);

    [Benchmark]
    public void QueryRefAction_SparseSingleComponent() => _sparseStore.QueryRef<BenchA, RefAction>(0f, ref _refAction);

    private static DataStore CreateSparse()
    {
        DataStore store = new();
        for (var i = 0; i < SparseCount; i++)
        {
            store.Alloc(new BenchA(i), new BenchB(i));
        }

        return store;
    }

    private static DataStore CreateDense()
    {
        DataStore store = new();
        for (var i = 0; i < DenseCount; i++)
        {
            store.Alloc(new BenchA(i));
        }

        return store;
    }

    private static DataStore CreateMixed()
    {
        DataStore store = new();
        for (var i = 0; i < MixedWideCount; i++)
        {
            store.Alloc(new BenchA(i));
        }

        for (var i = 0; i < MixedNarrowCount; i++)
        {
            store.AddOrUpdate(i + 1, new BenchB(i));
        }

        return store;
    }

    private struct BenchA : IDataComponent
    {
        public int Value;

        public BenchA(int value) => Value = value;
    }

    private struct BenchB : IDataComponent
    {
        public int Value;

        public BenchB(int value) => Value = value;
    }

    private struct NoopAction : IForEach<BenchA>
    {
        public void Execute(float delta, DataStore store, int entity, in BenchA cleanupAudioPlayer)
        {
        }
    }

    private struct RefAction : IForEachRef<BenchA>
    {
        public void Execute(float delta, DataStore store, int entity, ref Ref<BenchA> component)
        {
            component.Write.Value = 1;
        }
    }
}
