using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using OpenTK.Mathematics;
using Swordfish.ECS;
using Swordfish.Engine.Physics;
using Swordfish.Library.Collections;
using Swordfish.Library.Diagnostics;
using Xunit;
using Xunit.Abstractions;

namespace Swordfish.Tests
{
    public class ChunkedDataStoreTests
    {
        private readonly ITestOutputHelper Output;

        public ChunkedDataStoreTests(ITestOutputHelper output)
        {
            Output = output;
        }

        [Fact]
        public void GetAt()
        {
            ChunkedDataStore store = new(1, 4);
            store.Add(new object[] { "a", "test", "data", "store" });

            string chunk = store.GetAt<string>(0, 2);

            Assert.Equal("data", chunk);
        }
    }
}
