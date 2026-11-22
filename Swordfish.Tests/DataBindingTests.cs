using System;
using Swordfish.Library.Types;
using Xunit;

namespace Swordfish.Tests;

public class DataBindingTests
{
    [Fact]
    public void SetUpdatesValueBeforeRaisingChanged()
    {
        var binding = new DataBinding<bool>(true);
        bool? observed = null;
        binding.Changed += (_, _) => observed = binding.Get();

        binding.Set(false);

        Assert.False(observed);
        Assert.False(binding.Get());
    }

    [Fact]
    public void SetReportsOldAndNewValues()
    {
        var binding = new DataBinding<int>(1);
        DataChangedEventArgs<int> observed = default;
        binding.Changed += (_, args) => observed = args;

        binding.Set(2);

        Assert.Equal(1, observed.OldValue);
        Assert.Equal(2, observed.NewValue);
    }

    [Fact]
    public void SetDoesNotRaiseChangedForTheSameValue()
    {
        var binding = new DataBinding<int>(1);
        var raised = 0;
        binding.Changed += (_, _) => raised++;

        binding.Set(1);

        Assert.Equal(0, raised);
    }

    [Fact]
    public void SetRaisesChangedWhenValueBecomesNull()
    {
        var binding = new DataBinding<string>("value");
        var raised = 0;
        binding.Changed += (_, _) => raised++;

        binding.Set(null);

        Assert.Equal(1, raised);
        Assert.Null(binding.Get());
    }
}
