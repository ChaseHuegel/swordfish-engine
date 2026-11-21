using WaywardBeyond.Client.Configuration;
using Xunit;

namespace WaywardBeyond.Tests;

public class VoxelSettingsTests
{
    [Fact]
    public void RebuildBudgetDefaultsToTenMilliseconds()
    {
        var settings = new VoxelSettings();

        Assert.Equal(10f, settings.RebuildBudgetMs.Get());
    }
}
