using WaywardBeyond.Client.UI.Settings;
using Xunit;

namespace Swordfish.Tests;

public class SettingsEntryTests
{
    [Fact]
    public void ToggleEntryTracksDirtyStateAndResetsToDefault()
    {
        var value = false;
        var entry = new ToggleEntry(
            id: "test.toggle",
            labelKey: "test.label",
            get: () => value,
            set: newValue => value = newValue,
            defaultValue: () => false
        );

        entry.CaptureBaseline();
        Assert.False(entry.IsDirty);

        entry.SetValue(true);
        Assert.True(entry.IsDirty);

        entry.ResetToDefault();
        Assert.False(value);
        Assert.False(entry.IsDirty);
    }

    [Fact]
    public void OptionListsIncludeCurrentValue()
    {
        Assert.Contains(72, SettingsDefaults.GetFrameLimits(72));
        Assert.Contains(1234f, SettingsDefaults.GetViewDistances(1234f));
        Assert.Contains(7, SettingsDefaults.GetAutosaveIntervals(7));
        Assert.Contains(12, SettingsDefaults.GetAoSamples(12));
        Assert.Contains(11, SettingsDefaults.GetBloomPasses(11));
    }

    [Fact]
    public void DefaultsSitInsideSliderRanges()
    {
        Assert.InRange(SettingsDefaults.FOV, SettingsDefaults.FOV_MIN, SettingsDefaults.FOV_MAX);
        Assert.InRange(SettingsDefaults.MOUSE_SENSITIVITY, SettingsDefaults.SENSITIVITY_MIN, SettingsDefaults.SENSITIVITY_MAX);
        Assert.InRange(SettingsDefaults.NAME_TAG_DISTANCE, SettingsDefaults.NAME_TAG_MIN, SettingsDefaults.NAME_TAG_MAX);
    }
}
