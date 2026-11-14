using WaywardBeyond.Client.UI.Settings;
using Xunit;

namespace Swordfish.Tests;

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

        Assert.Equal("Ghost (unavailable)", state.Value);
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
        var service = new FakePlaybackDeviceService();
        service.SetDevices("Speakers");
        SettingsCategory category = AudioSettingsCategory.Build(draft, service, new TestLocalization(), () => { });

        category.ResetToDefaults();

        Assert.Equal(string.Empty, draft.OutputDevice);
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
}
