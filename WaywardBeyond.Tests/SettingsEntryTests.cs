using System.Collections.Generic;
using WaywardBeyond.Client.UI.Settings;
using Xunit;

namespace WaywardBeyond.Tests;

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
        Assert.Contains(AoQuality.High, SettingsDefaults.GetAoQualities(AoQuality.High));
        Assert.Contains(AoQuality.Ultra, SettingsDefaults.GetAoQualities(AoQuality.Ultra));
        Assert.Contains(BloomQuality.High, SettingsDefaults.GetBloomQualities(BloomQuality.High));
        Assert.Contains(1.33f, SettingsDefaults.GetUiScales(1.33f));
        Assert.Contains(ShadowQuality.Custom, SettingsDefaults.GetShadowQualities(ShadowQuality.Custom));
        Assert.Contains(ParticleDensity.Custom, SettingsDefaults.GetParticleDensities(ParticleDensity.Custom));
    }

    [Fact]
    public void StepperOptionsProviderRefreshesWithoutRebuildingTheEntry()
    {
        var options = new List<string> { "A", "B" };
        var value = "A";
        var entry = new StepperEntry<string>(
            id: "test.stepper",
            labelKey: "test.label",
            get: () => value,
            set: newValue => value = newValue,
            defaultValue: () => "A",
            options: () => options,
            format: option => option
        );

        Assert.True(entry.Snapshot().CanIncrease);

        options.Add("C");

        Assert.True(entry.Snapshot().CanIncrease);
        Assert.Equal(1, entry.Step(2));
        Assert.Equal("C", value);
    }

    [Fact]
    public void SliderEntryStepsAndClampsAtTheRangeEdges()
    {
        var value = 0.5f;
        var entry = new SliderEntry(
            id: "test.slider",
            labelKey: "test.label",
            get: () => value,
            set: newValue => value = newValue,
            defaultValue: () => 0.5f,
            min: 0f,
            max: 1f,
            step: 0.05f,
            format: current => current.ToString()
        );

        Assert.Equal(1, entry.Step(1));
        Assert.Equal(0.55f, value, 5);
        Assert.Equal(-1, entry.Step(-1));
        Assert.Equal(0.5f, value, 5);

        value = 1f;
        Assert.Equal(0, entry.Step(1));
        Assert.Equal(1f, value);

        value = 0f;
        Assert.Equal(0, entry.Step(-1));
        Assert.Equal(0f, value);
    }

    [Fact]
    public void SliderEntryDragReportsOnlyValueChanges()
    {
        var value = 0.5f;
        var entry = new SliderEntry(
            id: "test.slider",
            labelKey: "test.label",
            get: () => value,
            set: newValue => value = newValue,
            defaultValue: () => 0.5f,
            min: 0f,
            max: 1f,
            step: 0.05f,
            format: current => current.ToString()
        );

        Assert.Equal(0, entry.TrySetSliderRatio(0.5f));
        Assert.Equal(1, entry.TrySetSliderRatio(0.75f));
        Assert.Equal(-1, entry.TrySetSliderRatio(0.25f));
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

    [Fact]
    public void SaoDefaultsMatchTheReference()
    {
        var sao = new Swordfish.Settings.SAOSettings();

        Assert.True(sao.Enabled.Get());
        Assert.Equal(8, sao.Samples.Get());
        Assert.Equal(0.5f, sao.ResolutionScale.Get());
        Assert.Equal(0.35f, sao.Radius.Get());
        Assert.Equal(1f, sao.Intensity.Get());
        Assert.Equal(0.02f, sao.Bias.Get());
        Assert.Equal(1.5f, sao.Power.Get());
        Assert.Equal(48f, sao.MaxDistance.Get());
        Assert.Equal(8f, sao.FadeRange.Get());
        Assert.Equal(2, sao.BlurPasses.Get());
    }

    [Fact]
    public void AoQuality_PresetsApplyAndDetectCorrectly()
    {
        var settings = new Swordfish.Settings.RenderSettings();

        AoQuality.Low.Apply(settings);
        Assert.Equal(4, settings.SAO.Samples.Get());
        Assert.Equal(0.25f, settings.SAO.ResolutionScale.Get());
        Assert.Equal(32f, settings.SAO.MaxDistance.Get());
        Assert.Equal(AoQuality.Low, AoQualityExtensions.FromSettings(settings.SAO));

        AoQuality.Medium.Apply(settings);
        Assert.Equal(8, settings.SAO.Samples.Get());
        Assert.Equal(0.5f, settings.SAO.ResolutionScale.Get());
        Assert.Equal(48f, settings.SAO.MaxDistance.Get());
        Assert.Equal(AoQuality.Medium, AoQualityExtensions.FromSettings(settings.SAO));

        AoQuality.High.Apply(settings);
        Assert.Equal(16, settings.SAO.Samples.Get());
        Assert.Equal(0.5f, settings.SAO.ResolutionScale.Get());
        Assert.Equal(64f, settings.SAO.MaxDistance.Get());
        Assert.Equal(AoQuality.High, AoQualityExtensions.FromSettings(settings.SAO));

        AoQuality.Ultra.Apply(settings);
        Assert.Equal(16, settings.SAO.Samples.Get());
        Assert.Equal(1f, settings.SAO.ResolutionScale.Get());
        Assert.Equal(float.MaxValue, settings.SAO.MaxDistance.Get());
        Assert.Equal(AoQuality.Ultra, AoQualityExtensions.FromSettings(settings.SAO));

        settings.SAO.Samples.Set(6);
        Assert.Equal(AoQuality.Custom, AoQualityExtensions.FromSettings(settings.SAO));
    }

    [Fact]
    public void AoQuality_PresetsLeaveArtKnobsAlone()
    {
        var settings = new Swordfish.Settings.RenderSettings();
        settings.SAO.Radius.Set(0.7f);
        settings.SAO.Intensity.Set(2f);
        settings.SAO.Bias.Set(0.05f);
        settings.SAO.Power.Set(2f);
        settings.SAO.FadeRange.Set(4f);
        settings.SAO.BlurPasses.Set(4);

        AoQuality[] presets = [AoQuality.Low, AoQuality.Medium, AoQuality.High, AoQuality.Ultra];
        foreach (AoQuality preset in presets)
        {
            preset.Apply(settings);

            Assert.Equal(0.7f, settings.SAO.Radius.Get());
            Assert.Equal(2f, settings.SAO.Intensity.Get());
            Assert.Equal(0.05f, settings.SAO.Bias.Get());
            Assert.Equal(2f, settings.SAO.Power.Get());
            Assert.Equal(4f, settings.SAO.FadeRange.Get());
            Assert.Equal(4, settings.SAO.BlurPasses.Get());
        }
    }

    [Fact]
    public void AoQuality_UltraIsIndependentOfTheFarPlane()
    {
        var settings = new Swordfish.Settings.RenderSettings();

        AoQuality.Ultra.Apply(settings);
        Assert.Equal(float.MaxValue, settings.SAO.MaxDistance.Get());
        Assert.Equal(AoQuality.Ultra, AoQualityExtensions.FromSettings(settings.SAO));

        settings.FarPlane.Set(500f);
        Assert.Equal(AoQuality.Ultra, AoQualityExtensions.FromSettings(settings.SAO));
    }

    [Fact]
    public void BloomQuality_FreshSettingsMatchTheDeclaredDefault()
    {
        var settings = new Swordfish.Settings.RenderSettings();
        SettingsDefaults.ApplyRenderDefaults(settings);

        Assert.Equal(
            SettingsDefaults.BLOOM_QUALITY,
            BloomQualityExtensions.FromSettings(settings.Bloom)
        );
    }

    [Fact]
    public void BloomQuality_PresetsRoundTripAndHandTunedValuesAreCustom()
    {
        var settings = new Swordfish.Settings.RenderSettings();
        BloomQuality[] presets = [BloomQuality.Low, BloomQuality.Medium, BloomQuality.High];

        foreach (BloomQuality preset in presets)
        {
            preset.Apply(settings);
            Assert.Equal(preset, BloomQualityExtensions.FromSettings(settings.Bloom));
        }

        settings.Bloom.Passes.Set(5);
        settings.Bloom.Intensity.Set(0.5f);
        Assert.Equal(BloomQuality.Custom, BloomQualityExtensions.FromSettings(settings.Bloom));
    }

    [Fact]
    public void BloomQuality_SavedRenderConfigKeepsItsQuality()
    {
        string path = System.IO.Path.GetTempFileName();

        try
        {
            var settings = new Swordfish.Settings.RenderSettings
            {
                Path = new Swordfish.Library.IO.PathInfo(path),
            };
            BloomQuality.High.Apply(settings);

            SettingsDefaults.ApplyRenderDefaults(settings);

            Assert.Equal(BloomQuality.High, BloomQualityExtensions.FromSettings(settings.Bloom));
        }
        finally
        {
            System.IO.File.Delete(path);
        }
    }

    [Fact]
    public void ParticleDensity_PresetsApplyAndDetectCorrectly()
    {
        var settings = new Swordfish.Settings.RenderSettings();

        ParticleDensity.Off.Apply(settings);
        Assert.False(settings.Particles.Enabled.Get());
        Assert.Equal(ParticleDensity.Off, ParticleDensityExtensions.FromSettings(settings.Particles));

        ParticleDensity.Low.Apply(settings);
        Assert.True(settings.Particles.Enabled.Get());
        Assert.Equal(0.25f, settings.Particles.Density.Get());
        Assert.Equal(ParticleDensity.Low, ParticleDensityExtensions.FromSettings(settings.Particles));

        ParticleDensity.Medium.Apply(settings);
        Assert.True(settings.Particles.Enabled.Get());
        Assert.Equal(0.5f, settings.Particles.Density.Get());
        Assert.Equal(ParticleDensity.Medium, ParticleDensityExtensions.FromSettings(settings.Particles));

        ParticleDensity.High.Apply(settings);
        Assert.True(settings.Particles.Enabled.Get());
        Assert.Equal(0.75f, settings.Particles.Density.Get());
        Assert.Equal(ParticleDensity.High, ParticleDensityExtensions.FromSettings(settings.Particles));

        ParticleDensity.Ultra.Apply(settings);
        Assert.True(settings.Particles.Enabled.Get());
        Assert.Equal(1f, settings.Particles.Density.Get());
        Assert.Equal(ParticleDensity.Ultra, ParticleDensityExtensions.FromSettings(settings.Particles));

        settings.Particles.Density.Set(1.5f);
        Assert.Equal(ParticleDensity.Custom, ParticleDensityExtensions.FromSettings(settings.Particles));

        ParticleDensity.Custom.Apply(settings);
        Assert.True(settings.Particles.Enabled.Get());
        Assert.Equal(1.5f, settings.Particles.Density.Get());
    }

    [Fact]
    public void ParticleDensity_FreshSettingsMatchTheDeclaredDefault()
    {
        Assert.Equal(ParticleDensity.Ultra, SettingsDefaults.PARTICLE_DENSITY);

        var settings = new Swordfish.Settings.RenderSettings();
        SettingsDefaults.ApplyRenderDefaults(settings);

        Assert.Equal(
            SettingsDefaults.PARTICLE_DENSITY,
            ParticleDensityExtensions.FromSettings(settings.Particles)
        );

        string path = System.IO.Path.GetTempFileName();
        try
        {
            var saved = new Swordfish.Settings.RenderSettings
            {
                Path = new Swordfish.Library.IO.PathInfo(path),
            };
            ParticleDensity.Low.Apply(saved);

            SettingsDefaults.ApplyRenderDefaults(saved);

            Assert.Equal(ParticleDensity.Low, ParticleDensityExtensions.FromSettings(saved.Particles));
        }
        finally
        {
            System.IO.File.Delete(path);
        }
    }
}
