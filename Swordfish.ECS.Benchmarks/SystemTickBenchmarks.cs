using System.Numerics;
using BenchmarkDotNet.Attributes;
using Swordfish.ECS;

namespace Swordfish.ECS.Benchmarks;

[MemoryDiagnoser]
public class SystemTickBenchmarks
{
    [Params(500, 50_000)]
    public int EntityCount;

    private DataStore _store;

    private readonly ForEachRef<TransformData, VelocityData> _delegateField;

    public SystemTickBenchmarks()
    {
        _delegateField = static (float delta, DataStore store, int entity, ref Ref<TransformData> transform, ref Ref<VelocityData> velocity) =>
        {
            ref Vector3 p = ref transform.Write.Position;
            p.X += velocity.Read.Velocity.X * delta;
            p.Y += velocity.Read.Velocity.Y * delta;
            p.Z += velocity.Read.Velocity.Z * delta;
        };
    }

    [GlobalSetup]
    public void Setup()
    {
        _store = new DataStore();
        for (var i = 0; i < EntityCount; i++)
        {
            _store.Alloc(new TransformData(), new VelocityData());
        }
    }

    [Benchmark(Baseline = true)]
    public void DelegateField_Tick()
    {
        _store.QueryRef<TransformData, VelocityData>(FIXED_DELTA, _delegateField);
    }

    [Benchmark]
    public void MethodGroup_Tick()
    {
        _store.QueryRef<TransformData, VelocityData>(FIXED_DELTA, TickMethod);
    }

    [Benchmark]
    public void Action_Tick()
    {
        TickAction action = default;
        _store.QueryRef<TransformData, VelocityData, TickAction>(FIXED_DELTA, ref action);
    }

    private void TickMethod(float delta, DataStore store, int entity, ref Ref<TransformData> transform, ref Ref<VelocityData> velocity)
    {
        ref Vector3 p = ref transform.Write.Position;
        p.X += velocity.Read.Velocity.X * delta;
        p.Y += velocity.Read.Velocity.Y * delta;
        p.Z += velocity.Read.Velocity.Z * delta;
    }

    private const float FIXED_DELTA = 0.016f;

    private struct TransformData : IDataComponent
    {
        public Vector3 Position;
    }

    private struct VelocityData : IDataComponent
    {
        public Vector3 Velocity;
    }

    private struct TickAction : IForEachRef<TransformData, VelocityData>
    {
        public void Execute(float delta, DataStore store, int entity, ref Ref<TransformData> transform, ref Ref<VelocityData> velocity)
        {
            ref Vector3 p = ref transform.Write.Position;
            p.X += velocity.Read.Velocity.X * delta;
            p.Y += velocity.Read.Velocity.Y * delta;
            p.Z += velocity.Read.Velocity.Z * delta;
        }
    }
}