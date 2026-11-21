using System.Globalization;
using Swordfish.Library.Globalization;

namespace WaywardBeyond.Tests;

internal sealed class TestLocalization : ILocalization
{
    public string? GetString(string value)
    {
        return value switch
        {
            "ui.button.home.continue" => "Continue",
            "ui.button.home.singleplayer" => "Singleplayer",
            "ui.button.home.multiplayer" => "Multiplayer",
            "ui.button.settings" => "Settings",
            "ui.button.home.credits" => "Credits",
            "ui.button.home.quit" => "Quit",
            "ui.settings.value.systemDefault" => "System default",
            "ui.settings.value.unavailable" => "Unavailable",
            "ui.label.level.mode.creative" => "Creative",
            "ui.label.level.mode.adventure" => "Adventure",
            _ => value,
        };
    }

    public string? GetString(string value, string cultureName) => GetString(value);

    public string? GetString(string value, CultureInfo cultureInfo) => GetString(value);
}
