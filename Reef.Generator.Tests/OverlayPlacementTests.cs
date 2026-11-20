using Xunit;

namespace Reef.Generator.Tests;

public class OverlayPlacementTests
{
    private const string COMPANION = """
        using Reef;
        using Reef.UI;

        namespace ReefMarkupTest
        {
            public readonly record struct State(string TargetId, int Gap, bool Flip, IntVector2 Cursor, OverlayPlacement? Placement);

            public partial class V
            {
                private static OverlayPlacement PlaceAt(IntVector2 cursor) => OverlayPlacement.AtPoint(cursor);
            }
        }
        """;

    private static MarkupCompilation Compile(string markup)
    {
        return MarkupTestHost.Compile(markup, COMPANION);
    }

    [Fact]
    public void DeclarativeOverlayTargetEmitsForElementCall()
    {
        MarkupCompilation result = Compile("""
            <Reef xmlns="Reef" Class="V" Context="State" Namespace="ReefMarkupTest">
                <Element id="Button_Continue" width="100" height="40" />
                <Element overlay-target="Button_Continue"
                         overlay-side="right"
                         overlay-align="start"
                         overlay-gap="8"
                         overlay-margin="12"
                         overlay-flip="true"
                         overlay-clamp="false"
                         width="120"
                         height="60" />
            </Reef>
            """);

        Assert.Empty(result.Errors);
        Assert.Contains(
            "ui.OverlayPlacement = global::Reef.UI.OverlayPlacement.ForElement(\"Button_Continue\", side: global::Reef.UI.OverlaySide.Right, align: global::Reef.UI.OverlayAlign.Start, gap: 8, viewportMargin: 12, flip: true, clamp: false);",
            result.GeneratedText
        );
    }

    [Fact]
    public void DynamicOverlayTargetAndModifiersBindExpressions()
    {
        MarkupCompilation result = Compile("""
            <Reef xmlns="Reef" Class="V" Context="State" Namespace="ReefMarkupTest">
                <Element overlay-target="@context.TargetId"
                         overlay-side="bottom"
                         overlay-align="center"
                         overlay-gap="@context.Gap"
                         overlay-flip="@context.Flip"
                         width="120"
                         height="60" />
            </Reef>
            """);

        Assert.Empty(result.Errors);
        Assert.Contains(
            "ui.OverlayPlacement = global::Reef.UI.OverlayPlacement.ForElement(context.TargetId, side: global::Reef.UI.OverlaySide.Bottom, align: global::Reef.UI.OverlayAlign.Center, gap: context.Gap, flip: context.Flip);",
            result.GeneratedText
        );
    }

    [Fact]
    public void OverlayPlacementExpressionBindsFactoryAndAppliesOverrides()
    {
        MarkupCompilation result = Compile("""
            <Reef xmlns="Reef" Class="V" Context="State" Namespace="ReefMarkupTest">
                <Element overlay-placement="@PlaceAt(context.Cursor)"
                         overlay-gap="4"
                         width="120"
                         height="60" />
                <Element overlay-placement="@context.Placement"
                         width="120"
                         height="60" />
            </Reef>
            """);

        Assert.Empty(result.Errors);
        Assert.Contains("ui.OverlayPlacement = PlaceAt(context.Cursor);", result.GeneratedText);
        Assert.Contains("Gap = 4", result.GeneratedText);
        Assert.Contains("ui.OverlayPlacement = context.Placement;", result.GeneratedText);
    }

    [Fact]
    public void InvalidOverlaySideReportsDiagnostic()
    {
        MarkupCompilation result = Compile("""
            <Reef xmlns="Reef" Class="V" Context="State" Namespace="ReefMarkupTest">
                <Element overlay-target="slot" overlay-side="diagonal" />
            </Reef>
            """);

        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public void InvalidOverlayAlignReportsDiagnostic()
    {
        MarkupCompilation result = Compile("""
            <Reef xmlns="Reef" Class="V" Context="State" Namespace="ReefMarkupTest">
                <Element overlay-target="slot" overlay-align="middle" />
            </Reef>
            """);

        Assert.NotEmpty(result.Errors);
    }
}
