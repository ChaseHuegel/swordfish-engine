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
        Assert.Contains(ShadowQuality.Custom, SettingsDefaults.GetShadowQualities(ShadowQuality.Custom));
    }

    [Fact]
    public void DefaultsSitInsideSliderRanges()
    {
        Assert.InRange(SettingsDefaults.FOV, SettingsDefaults.FOV_MIN, SettingsDefaults.FOV_MAX);
        Assert.InRange(SettingsDefaults.MOUSE_SENSITIVITY, SettingsDefaults.SENSITIVITY_MIN, SettingsDefaults.SENSITIVITY_MAX);
        Assert.InRange(SettingsDefaults.NAME_TAG_DISTANCE, SettingsDefaults.NAME_TAG_MIN, SettingsDefaults.NAME_TAG_MAX);
    }

    [Fact]
    public void ShadowQuality_PresetsApplyAndDetectCorrectly()
    {
        var settings = new Swordfish.Settings.RenderSettings();

        ShadowQuality.Off.Apply(settings);
        Assert.False(settings.Shadows.Enabled.Get());
        Assert.Equal(ShadowQuality.Off, ShadowQualityExtensions.FromSettings(settings.Shadows));

        ShadowQuality.Low.Apply(settings);
        Assert.True(settings.Shadows.Enabled.Get());
        Assert.Equal(512, settings.Shadows.Resolution.Get());
        Assert.Equal(2, settings.Shadows.CascadeCount.Get());
        Assert.Equal(2, settings.Shadows.MaxPointShadows.Get());
        Assert.Equal(Swordfish.Settings.ShadowFilterMode.Sharp, settings.Shadows.FilterMode.Get());
        Assert.Equal(48f, settings.Shadows.MaxDistance.Get());
        Assert.Equal(ShadowQuality.Low, ShadowQualityExtensions.FromSettings(settings.Shadows));

        ShadowQuality.Medium.Apply(settings);
        Assert.True(settings.Shadows.Enabled.Get());
        Assert.Equal(1024, settings.Shadows.Resolution.Get());
        Assert.Equal(3, settings.Shadows.CascadeCount.Get());
        Assert.Equal(3, settings.Shadows.MaxPointShadows.Get());
        Assert.Equal(Swordfish.Settings.ShadowFilterMode.Soft, settings.Shadows.FilterMode.Get());
        Assert.Equal(96f, settings.Shadows.MaxDistance.Get());
        Assert.Equal(ShadowQuality.Medium, ShadowQualityExtensions.FromSettings(settings.Shadows));

        ShadowQuality.High.Apply(settings);
        Assert.True(settings.Shadows.Enabled.Get());
        Assert.Equal(2048, settings.Shadows.Resolution.Get());
        Assert.Equal(4, settings.Shadows.CascadeCount.Get());
        Assert.Equal(4, settings.Shadows.MaxPointShadows.Get());
        Assert.Equal(Swordfish.Settings.ShadowFilterMode.Soft, settings.Shadows.FilterMode.Get());
        Assert.Equal(160f, settings.Shadows.MaxDistance.Get());
        Assert.Equal(ShadowQuality.High, ShadowQualityExtensions.FromSettings(settings.Shadows));

        ShadowQuality.Ultra.Apply(settings);
        Assert.True(settings.Shadows.Enabled.Get());
        Assert.Equal(4096, settings.Shadows.Resolution.Get());
        Assert.Equal(4, settings.Shadows.CascadeCount.Get());
        Assert.Equal(8, settings.Shadows.MaxPointShadows.Get());
        Assert.Equal(Swordfish.Settings.ShadowFilterMode.Soft, settings.Shadows.FilterMode.Get());
        Assert.Equal(256f, settings.Shadows.MaxDistance.Get());
        Assert.Equal(ShadowQuality.Ultra, ShadowQualityExtensions.FromSettings(settings.Shadows));

        settings.Shadows.Resolution.Set(1536);
        Assert.Equal(ShadowQuality.Custom, ShadowQualityExtensions.FromSettings(settings.Shadows));
    }
}
