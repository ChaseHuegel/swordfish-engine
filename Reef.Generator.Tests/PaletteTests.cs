using Xunit;

namespace Reef.Generator.Tests;

public class PaletteTests
{
    private const string STYLESHEET = """
        :root {
            --wb-text: #f0f3f6;
            --wb-panel: #111723f0;
            --wb-font-ui: "Salmon Mono 9 Regular";
            --wb-slot-size: 48px;
            --other-value: #ff0000;
        }
        """;

    [Fact]
    public void PaletteEmitsTypedMembersForThePrefix()
    {
        MarkupCompilation result = Compile("wb");

        Assert.Empty(result.Errors);
        Assert.Contains("internal static class WbPalette", result.GeneratedText);
        Assert.Contains(
            "public static readonly global::System.Numerics.Vector4 Text = new global::System.Numerics.Vector4(",
            result.GeneratedText);
        Assert.Contains("public static readonly string FontUi = \"Salmon Mono 9 Regular\";", result.GeneratedText);
        Assert.Contains("public static readonly int SlotSize = 48;", result.GeneratedText);
        Assert.DoesNotContain("OtherValue", result.GeneratedText);
        Assert.DoesNotContain("public void Render", result.GeneratedText);
        Assert.DoesNotContain("ReefComponent", result.GeneratedText);
    }

    [Fact]
    public void PaletteHonoursPublicAccess()
    {
        MarkupCompilation result = MarkupTestHost.Compile("""
            <Reef xmlns="Reef" Class="WbPalette" Access="public" Palette="wb" Namespace="ReefMarkupTest" />
            """, "", STYLESHEET);

        Assert.Empty(result.Errors);
        Assert.Contains("public static class WbPalette", result.GeneratedText);
    }

    [Fact]
    public void PaletteRejectsElements()
    {
        MarkupCompilation result = MarkupTestHost.Compile("""
            <Reef xmlns="Reef" Class="WbPalette" Palette="wb" Namespace="ReefMarkupTest">
                <Element width="10" height="10" />
            </Reef>
            """, "", STYLESHEET);

        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "RF0035");
    }

    [Fact]
    public void PaletteRejectsAnInvalidPrefix()
    {
        MarkupCompilation result = Compile("WB!");

        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "RF0035");
    }

    [Fact]
    public void PaletteRejectsAnUnsupportedValue()
    {
        MarkupCompilation result = MarkupTestHost.Compile("""
            <Reef xmlns="Reef" Class="WbPalette" Palette="wb" Namespace="ReefMarkupTest" />
            """, "", ":root { --wb-bad: 4px solid red; }");

        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "RF0035");
    }

    [Fact]
    public void PaletteRejectsACollidingMemberName()
    {
        MarkupCompilation result = MarkupTestHost.Compile("""
            <Reef xmlns="Reef" Class="WbPalette" Palette="wb" Namespace="ReefMarkupTest" />
            """, "", ":root { --wb-text: #fff; --wb-Text: #fff; }");

        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "RF0035");
    }

    private static MarkupCompilation Compile(string prefix)
    {
        return MarkupTestHost.Compile(
            "<Reef xmlns=\"Reef\" Class=\"WbPalette\" Palette=\"" + prefix + "\" Namespace=\"ReefMarkupTest\" />",
            "",
            STYLESHEET);
    }
}
