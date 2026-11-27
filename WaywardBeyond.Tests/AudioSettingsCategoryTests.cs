using Swordfish.Tests;
using WaywardBeyond.Client.UI.Settings;
using Xunit;

namespace WaywardBeyond.Tests;

public class AudioSettingsCategoryTests
{
    [Fact]
    public void OutputDeviceRowStartsWithSystemDefault()
    {
        SettingsDraft draft = SettingsDraft.CreateDefault(new FakeWindowContext());
        var service = new FakePlaybackDeviceService();
        service.SetDevices("Speakers", "Headphones");
        SettingsCategory category = AudioSettingsCategory.Build(draft, service, new TestLocalization(), () => { });

        StepperEntry<string> row = FindOutputDeviceRow(category);
        SettingsRowState state = row.Snapshot();

        Assert.Equal("System default", state.Value);
        Assert.False(state.CanDecrease);
        Assert.True(state.CanIncrease);
    }

    [Fact]
    public void OutputDeviceRowStepsThroughEveryDevice()
    {
        SettingsDraft draft = SettingsDraft.CreateDefault(new FakeWindowContext());
        var service = new FakePlaybackDeviceService();
        service.SetDevices("Speakers", "Headphones");
        SettingsCategory category = AudioSettingsCategory.Build(draft, service, new TestLocalization(), () => { });

        StepperEntry<string> row = FindOutputDeviceRow(category);

        Assert.Equal(1, row.Step(1));
        Assert.Equal("Speakers", draft.OutputDevice);

        Assert.Equal(1, row.Step(1));
        Assert.Equal("Headphones", draft.OutputDevice);
    }

    [Fact]
    public void OutputDeviceRowKeepsMissingSelection()
    {
        SettingsDraft draft = SettingsDraft.CreateDefault(new FakeWindowContext());
        draft.OutputDevice = "Ghost";
        var service = new FakePlaybackDeviceService();
        service.SetDevices("Speakers");
        SettingsCategory category = AudioSettingsCategory.Build(draft, service, new TestLocalization(), () => { });

        StepperEntry<string> row = FindOutputDeviceRow(category);
        SettingsRowState state = row.Snapshot();

        Assert.Equal("Ghost (Unavailable)", state.Value);
        Assert.True(state.CanDecrease);
        Assert.False(state.CanIncrease);

        Assert.Equal(-1, row.Step(-1));
        Assert.Equal("Speakers", draft.OutputDevice);
    }

    [Fact]
    public void ResetRestoresSystemDefault()
    {
        SettingsDraft draft = SettingsDraft.CreateDefault(new FakeWindowContext());
        draft.OutputDevice = "Speakers";
        draft.PausedVolume = 0.25f;
        draft.BackgroundVolume = 0.75f;
        var service = new FakePlaybackDeviceService();
        service.SetDevices("Speakers");
        SettingsCategory category = AudioSettingsCategory.Build(draft, service, new TestLocalization(), () => { });

        category.ResetToDefaults();

        Assert.Equal(string.Empty, draft.OutputDevice);
        Assert.Equal(0.5f, draft.PausedVolume);
        Assert.Equal(0f, draft.BackgroundVolume);
    }

    [Fact]
    public void PausedAndBackgroundSlidersUseExpectedDefaultsAndSteps()
    {
        SettingsDraft draft = SettingsDraft.CreateDefault(new FakeWindowContext());
        var service = new FakePlaybackDeviceService();
        SettingsCategory category = AudioSettingsCategory.Build(draft, service, new TestLocalization(), () => { });

        SliderEntry paused = FindSliderRow(category, "Settings.PausedVolume");
        SliderEntry background = FindSliderRow(category, "Settings.BackgroundVolume");

        Assert.Equal("50%", paused.Snapshot().Display);
        Assert.Equal("0%", background.Snapshot().Display);

        Assert.Equal(-1, paused.Step(-1));
        Assert.Equal(0.45f, draft.PausedVolume, precision: 3);

        Assert.Equal(1, background.Step(1));
        Assert.Equal(0.05f, draft.BackgroundVolume, precision: 3);
    }

    private static StepperEntry<string> FindOutputDeviceRow(SettingsCategory category)
    {
        foreach (SettingsEntry entry in category.Entries)
        {
            if (entry is StepperEntry<string> row && row.Id == "Settings.OutputDevice")
            {
                return row;
            }
        }

        throw new Xunit.Sdk.XunitException("No output device row was found.");
    }

    private static SliderEntry FindSliderRow(SettingsCategory category, string id)
    {
        foreach (SettingsEntry entry in category.Entries)
        {
            if (entry is SliderEntry row && row.Id == id)
            {
                return row;
            }
        }

        throw new Xunit.Sdk.XunitException($"No slider row \"{id}\" was found.");
    }
}
