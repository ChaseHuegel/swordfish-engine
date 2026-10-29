using Xunit;

namespace Reef.Generator.Tests;

public class StyleEmissionTests
{
    [Fact]
    public void PseudoStatesEmitImmediateModeBranches()
    {
        MarkupCompilation result = MarkupTestHost.Compile("""
            <Reef xmlns="Reef" Class="V">
                <Element id="panel" class="panel" width="10" height="10" />
            </Reef>
            """, """
            namespace ReefMarkupTest
            {
                public partial class V
                {
                }
            }
            """, """
            .panel { background: #101010; }
            .panel:hover { background: #202020; }
            .panel:active { background: #303030; }
            .panel:focus { border-color: #ffffff; }
            .panel:disabled { background: #404040; }
            """);
        Assert.Empty(result.Errors);
        Assert.Contains(".Hovering()", result.GeneratedText);
        Assert.Contains(".Held()", result.GeneratedText);
        Assert.Contains(".Focused()", result.GeneratedText);
        Assert.Contains("__disabled", result.GeneratedText);
    }

    [Fact]
    public void DynamicClassDirectiveEmitsConditionalAssignment()
    {
        MarkupCompilation result = MarkupTestHost.Compile("""
            <Reef xmlns="Reef" xmlns:class="Reef.Class" Class="V" Context="Ctx">
                <Element id="panel" class="panel" class:accent="@context.Accent" width="10" height="10" />
            </Reef>
            """, """
            namespace ReefMarkupTest
            {
                public sealed class Ctx
                {
                    public bool Accent { get; set; }
                }

                public partial class V
                {
                }
            }
            """, ".accent { color: #ffff00; } .panel { background: #101010; }");
        Assert.Empty(result.Errors);
        Assert.Contains("if (context.Accent)", result.GeneratedText);
    }

    [Fact]
    public void ImportantOverridesVariableAcrossFiles()
    {
        MarkupCompilation result = MarkupTestHost.CompileFiles(
        [
            ("V.rml", """
                <Reef xmlns="Reef" Class="V" Namespace="ReefMarkupTest">
                    <Element class="a" width="10" height="10" />
                </Reef>
                """),
            ("Base.rss", ":root { --accent: #ff0000; } .a { color: var(--accent); }"),
            ("Game.rss", ":root { --accent: #0000ff !important; }"),
        ], """
            namespace ReefMarkupTest
            {
                public partial class V
                {
                }
            }
            """);
        Assert.Empty(result.Errors);
        Assert.Contains("new global::System.Numerics.Vector4(0f, 0f, 1f, 1f)", result.GeneratedText);
    }

    [Fact]
    public void ElementBackgroundMapsToFillAndTextBackgroundStaysBackground()
    {
        MarkupCompilation result = MarkupTestHost.Compile("""
            <Reef xmlns="Reef" Class="V">
                <Element background="#ff0000" width="10" height="10" />
                <Text text="x" background="#00ff00" />
            </Reef>
            """, """
            namespace ReefMarkupTest
            {
                public partial class V
                {
                }
            }
            """);
        Assert.Empty(result.Errors);
        Assert.Contains("ui.Color = new global::System.Numerics.Vector4(1f, 0f, 0f, 1f);", result.GeneratedText);
        Assert.Contains("ui.BackgroundColor = new global::System.Numerics.Vector4(0f, 1f, 0f, 1f);", result.GeneratedText);
    }

    [Fact]
    public void MultipleBaseClassInheritanceFlattensInDeclarationOrder()
    {
        MarkupCompilation result = MarkupTestHost.Compile("""
            <Reef xmlns="Reef" Class="V">
                <Element class="child" />
            </Reef>
            """, """
            namespace ReefMarkupTest
            {
                public partial class V
                {
                }
            }
            """, """
            .base1 {
                width: 100px;
                color: #ff0000;
            }

            .base2 {
                color: #0000ff;
                height: 50px;
            }

            .child {
                --extends: base1 base2;
                border: 1px #ffffff;
            }
            """);
        Assert.Empty(result.Errors);
        // Inherits width from base1
        Assert.Contains("Width = new global::Reef.Constraints.Fixed(100)", result.GeneratedText);
        // Inherits height from base2
        Assert.Contains("Height = new global::Reef.Constraints.Fixed(50)", result.GeneratedText);
        // base2 overrides base1 color (#0000ff instead of #ff0000)
        Assert.Contains("ui.Color = new global::System.Numerics.Vector4(0f, 0f, 1f, 1f);", result.GeneratedText);
        Assert.DoesNotContain("ui.Color = new global::System.Numerics.Vector4(1f, 0f, 0f, 1f);", result.GeneratedText);
        // child defines border (coalesced)
        Assert.Contains("ui.Border = ui.Border with { Thickness = 1, Color = new global::System.Numerics.Vector4(1f, 1f, 1f, 1f) };", result.GeneratedText);
    }

    [Fact]
    public void MultiFileRulesetCascadesAndMergesInDiscoveryOrder()
    {
        MarkupCompilation result = MarkupTestHost.CompileFiles(
        [
            ("V.rml", """
                <Reef xmlns="Reef" Class="V" Namespace="ReefMarkupTest">
                    <Element class="card" />
                </Reef>
                """),
            ("BaseTheme.rss", ".card { padding: 12px; color: #ff0000; }"),
            ("GameTheme.rss", ".card { color: #00ff00; }"),
        ], """
            namespace ReefMarkupTest
            {
                public partial class V
                {
                }
            }
            """);
        Assert.Empty(result.Errors);
        // Inherits padding from BaseTheme.rss
        Assert.Contains("ui.Padding = new global::Reef.UI.Padding(left: 12, top: 12, right: 12, bottom: 12);", result.GeneratedText);
        // GameTheme.rss overrides color (#00ff00 instead of #ff0000)
        Assert.Contains("ui.Color = new global::System.Numerics.Vector4(0f, 1f, 0f, 1f);", result.GeneratedText);
        Assert.DoesNotContain("ui.Color = new global::System.Numerics.Vector4(1f, 0f, 0f, 1f);", result.GeneratedText);
    }

    [Fact]
    public void CompositeAssignmentsAreCoalescedIntoSingleWithExpressions()
    {
        MarkupCompilation result = MarkupTestHost.Compile("""
            <Reef xmlns="Reef" Class="Notice">
                <Element width="100%" height="100%" min-width="520" max-width="760" anchors="center">
                    <Text text="Hello" font="'Salmon Mono 9 Regular'" size="24" align="center" valign="center" width="fill" height="30" />
                </Element>
            </Reef>
            """, """
            namespace ReefMarkupTest
            {
                public partial class Notice
                {
                }
            }
            """);
        Assert.Empty(result.Errors);
        // Element constraints are emitted in a single compound with expression
        Assert.Contains("ui.Constraints = ui.Constraints with { Width = new global::Reef.UI.Relative(1f), Height = new global::Reef.UI.Relative(1f), MinWidth = 520, MaxWidth = 760, Anchors = global::Reef.UI.Anchors.Center };", result.GeneratedText);
        // Text font options are coalesced together without interleaving
        Assert.Contains("ui.FontOptions = ui.FontOptions with { ID = \"Salmon Mono 9 Regular\", Size = 24, HorizontalAlignment = global::Reef.UI.HorizontalAlignment.Center, VerticalAlignment = global::Reef.UI.VerticalAlignment.Center };", result.GeneratedText);
        // Text constraints are coalesced together without interleaving
        Assert.Contains("ui.Constraints = ui.Constraints with { Width = new global::Reef.Constraints.Fill(), Height = new global::Reef.Constraints.Fixed(30) };", result.GeneratedText);
    }
}
