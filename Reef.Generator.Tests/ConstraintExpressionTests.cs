using Xunit;

namespace Reef.Generator.Tests;

public class ConstraintExpressionTests
{
    [Fact]
    public void ConstraintExpressionPassesThrough()
    {
        MarkupCompilation result = MarkupTestHost.Compile("""
            <Reef xmlns="Reef" Class="V" Context="Ctx">
                <Element width="@context.Width" height="@context.Height" />
            </Reef>
            """, """
            namespace ReefMarkupTest
            {
                public sealed class Ctx
                {
                    public global::Reef.Constraints.IConstraint Width { get; set; } = new global::Reef.Constraints.Fill();
                    public global::Reef.Constraints.IConstraint Height { get; set; } = new global::Reef.Constraints.Fill();
                }

                public partial class V
                {
                }
            }
            """);

        Assert.Empty(result.Errors);
        Assert.Contains("Width = context.Width", result.GeneratedText);
        Assert.Contains("Height = context.Height", result.GeneratedText);
        Assert.DoesNotContain("new global::Reef.Constraints.Fixed(context.Width)", result.GeneratedText);
    }

    [Fact]
    public void FontSizeExpressionPassesThrough()
    {
        MarkupCompilation result = MarkupTestHost.Compile("""
            <Reef xmlns="Reef" Class="V" Context="Ctx">
                <Text text="Ada" font-size="@context.Size" />
            </Reef>
            """, """
            namespace ReefMarkupTest
            {
                public sealed class Ctx
                {
                    public int Size { get; set; } = 16;
                }

                public partial class V
                {
                }
            }
            """);

        Assert.Empty(result.Errors);
        Assert.Contains("Size = context.Size", result.GeneratedText);
    }

    [Fact]
    public void IntegerConstraintExpressionStillUsesFixed()
    {
        MarkupCompilation result = MarkupTestHost.Compile("""
            <Reef xmlns="Reef" Class="V" Context="Ctx">
                <Element width="@context.Width" />
            </Reef>
            """, """
            namespace ReefMarkupTest
            {
                public sealed class Ctx
                {
                    public int Width { get; set; } = 10;
                }

                public partial class V
                {
                }
            }
            """);

        Assert.Empty(result.Errors);
        Assert.Contains("new global::Reef.Constraints.Fixed(context.Width)", result.GeneratedText);
    }
}
