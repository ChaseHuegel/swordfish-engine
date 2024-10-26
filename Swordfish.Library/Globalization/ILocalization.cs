#nullable enable
using System.Globalization;
// ReSharper disable UnusedMember.Global

namespace Swordfish.Library.Globalization;

public interface ILocalization
{
    public string? GetString(string value);

    public string? GetString(string value, string cultureName);

    public string? GetString(string value, CultureInfo cultureInfo);
}