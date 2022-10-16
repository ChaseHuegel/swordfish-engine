using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using OpenTK.Mathematics;
using Swordfish.ECS;
using Swordfish.Engine.Physics;
using Swordfish.Library.Diagnostics;
using Swordfish.Library.Types;
using Xunit;
using Xunit.Abstractions;

namespace Swordfish.Tests
{
    public class ECSTests : TestBase
    {
        private readonly ECSContext ECSContext;

        public ECSTests(ITestOutputHelper output) : base(output)
        {
            ECSContext = new ECSContext();
            ECSContext.Start();
            ECSContext.CreateEntity();
        }

        [Fact]
        public void BenchmarkComponentRetrieval()
        {
            BenchmarkGetAtLong();
            BenchmarkGetAtShort();
            BenchmarkGetComponent();
        }

        [Fact]
        public void BenchmarkGetAtLong()
        {
            Entity entity = ECSContext.GetEntities()[0];

            //  Warm up
            entity.World.Store.GetAt(0, TransformComponent.DefaultIndex);
            entity.World.Store.GetAt<TransformComponent>(entity.Ptr, TransformComponent.DefaultIndex);

            TransformComponent component;

            Stopwatch sw = Stopwatch.StartNew();
            component = entity.World.Store.GetAt<TransformComponent>(entity.Ptr, TransformComponent.DefaultIndex);
            Output.WriteLine($"World.Store.GetAt<T>: {sw.Elapsed.TotalMilliseconds} ms");
        }

        [Fact]
        public void BenchmarkGetAtShort()
        {
            Entity entity = ECSContext.GetEntities()[0];

            //  Warm up
            entity.World.Store.GetAt(0, TransformComponent.DefaultIndex);
            entity.World.Store.GetAt<TransformComponent>(entity.Ptr, TransformComponent.DefaultIndex);

            TransformComponent component;

            Stopwatch sw = Stopwatch.StartNew();
            component = entity.Store.GetAt<TransformComponent>(entity.Ptr, TransformComponent.DefaultIndex);
            Output.WriteLine($"Store.GetAt<T>: {sw.Elapsed.TotalMilliseconds} ms");
        }

        [Fact]
        public void BenchmarkGetComponent()
        {
            Entity entity = ECSContext.GetEntities()[0];

            //  Warm up
            entity.World.Store.GetAt(0, TransformComponent.DefaultIndex);
            entity.World.Store.GetAt<TransformComponent>(entity.Ptr, TransformComponent.DefaultIndex);

            TransformComponent component;

            Stopwatch sw = Stopwatch.StartNew();
            component = entity.GetComponent<TransformComponent>(TransformComponent.DefaultIndex);
            Output.WriteLine($"GetComponent<T>: {sw.Elapsed.TotalMilliseconds} ms");
        }
    }
}
