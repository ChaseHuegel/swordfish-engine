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
}
