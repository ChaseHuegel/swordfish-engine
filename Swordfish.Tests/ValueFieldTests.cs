using Swordfish.Library.Types;
using Xunit;
using Xunit.Abstractions;

namespace Swordfish.Tests
{
    public class ValueFieldTests : TestBase
    {
        public ValueFieldTests(ITestOutputHelper output) : base(output) { }

        [Fact]
        public void SetMaxValueDoesScale()
        {
            ValueField valueField = new("test", 10f, 10f);
            valueField.MaxValue = 15f;
            Assert.Equal(15f, valueField.MaxValue);
            Assert.Equal(15f, valueField.Value);

            valueField = new("test", 5f, 10f);
            valueField.MaxValue = 20f;
            Assert.Equal(20f, valueField.MaxValue);
            Assert.Equal(10f, valueField.Value);
        }

        [Fact]
        public void SetMaxValueFromUncappedDoesNotScale()
        {
            ValueField valueField = new("test", 10f);
            valueField.MaxValue = 15f;
            Assert.Equal(10f, valueField.Value);
        }

        [Fact]
        public void SetMaxValueToUncappedDoesNotScale()
        {
            ValueField valueField = new("test", 10f, 10f);
            valueField.MaxValue = 0f;
            Assert.Equal(10f, valueField.Value);
        }
    }
}
