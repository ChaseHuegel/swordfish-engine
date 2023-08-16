using Swordfish.Library.Collections;
using Xunit;
using Xunit.Abstractions;

namespace Swordfish.Tests
{
    public class ChunkedDataStoreTests : TestBase
    {
        public ChunkedDataStoreTests(ITestOutputHelper output) : base(output)
        {
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
