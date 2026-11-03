using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using WaywardBeyond.Client.UI.Theme;
using Xunit;

namespace Swordfish.Tests;

public class PaletteTests
{
    private const string PREFIX = "wb-";

    [Fact]
    public void PaletteCarriesEveryThemeToken()
    {
        string path = Path.Combine(RepositoryDirectory(), "WaywardBeyond.Client", "UI", "Theme", "Theme.rss");
        string stylesheet = File.ReadAllText(path);

        var count = 0;
        foreach (Match match in Regex.Matches(stylesheet, @"--(wb-[a-z0-9-]+)\s*:"))
        {
            string member = ToPascalCase(match.Groups[1].Value.Substring(PREFIX.Length));
            FieldInfo? field = typeof(WbPalette).GetField(member, BindingFlags.Public | BindingFlags.Static);
            Assert.True(field != null, "WbPalette is missing the member '" + member + "'.");
            count++;
        }

        Assert.True(count > 0, "No theme tokens were found.");
    }

    private static string ToPascalCase(string name)
    {
        var builder = new StringBuilder(name.Length);
        var upper = true;
        for (var i = 0; i < name.Length; i++)
        {
            char c = name[i];
            if (c == '-')
            {
                upper = true;
                continue;
            }

            builder.Append(upper ? char.ToUpperInvariant(c) : c);
            upper = false;
        }

        return builder.ToString();
    }

    private static string RepositoryDirectory([CallerFilePath] string path = "")
    {
        return Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path)!, ".."));
    }
}
