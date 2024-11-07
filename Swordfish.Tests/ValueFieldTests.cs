using Swordfish.Library.Types;
using Xunit;
using Xunit.Abstractions;

namespace Swordfish.Tests;

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

    [Fact]
    public void ModifiersAreOrdered()
    {
        ValueField valueField = new("test", 10f, 10f);
        valueField.AddModifier("div", Modifier.Divide, 1);
        valueField.AddModifier("sub", Modifier.Subtract, 1);
        valueField.AddModifier("mul", Modifier.Multiply, 1);
        valueField.AddModifier("add", Modifier.Addition, 1);

        var modifiers = valueField.GetModifiers();
        Assert.Equal(Modifier.Addition, modifiers[0].Modifier);
        Assert.Equal(Modifier.Subtract, modifiers[1].Modifier);
        Assert.Equal(Modifier.Multiply, modifiers[2].Modifier);
        Assert.Equal(Modifier.Divide, modifiers[3].Modifier);
    }

    [Fact]
    public void ModifiersApplyOrdered()
    {
        ValueField valueField = new("test", 10f, 20f);
        valueField.AddModifier("div", Modifier.Divide, 2);
        valueField.AddModifier("sub", Modifier.Subtract, 5);
        valueField.AddModifier("mul", Modifier.Multiply, 2);
        valueField.AddModifier("add", Modifier.Addition, 8);
        Assert.Equal(13, valueField.Value);
    }
}
