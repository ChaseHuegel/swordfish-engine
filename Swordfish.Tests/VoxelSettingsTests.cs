using WaywardBeyond.Client.Configuration;
using Xunit;

namespace Swordfish.Tests;

public class VoxelSettingsTests
{
    [Fact]
    public void RebuildBudgetDefaultsToTenMilliseconds()
    {
        var settings = new VoxelSettings();

        Assert.Equal(10f, settings.RebuildBudgetMs.Get());
    }
}
