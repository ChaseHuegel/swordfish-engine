using System.Globalization;
using Swordfish.Library.Globalization;

namespace Swordfish.Tests;

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
            _ => value,
        };
    }

    public string? GetString(string value, string cultureName) => GetString(value);

    public string? GetString(string value, CultureInfo cultureInfo) => GetString(value);
}
