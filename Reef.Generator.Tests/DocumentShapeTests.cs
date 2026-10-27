using Xunit;

namespace Reef.Generator.Tests;

public class DocumentShapeTests
{
    [Fact]
    public void EmptyViewGeneratesNoOpRender()
    {
        MarkupCompilation result = MarkupTestHost.Compile("""
            <Reef xmlns="Reef" Class="EmptyView" Namespace="ReefMarkupTest" />
            """, """
            namespace ReefMarkupTest
            {
                public partial class EmptyView
                {
                }
            }
            """);
        Assert.Empty(result.Errors);
        Assert.Contains("public void Render<TData>", result.GeneratedText);
        Assert.DoesNotContain("using (ui.Element", result.GeneratedText);
    }

    [Fact]
    public void NamespaceIsInferredFromFolder()
    {
        MarkupCompilation result = MarkupTestHost.CompileFiles(
        [
            ("Views/InferredView.rml", """
                <Reef xmlns="Reef" Class="InferredView">
                    <Element width="10" height="10" />
                </Reef>
                """),
        ], """
            namespace ReefMarkupTest.Views
            {
                public partial class InferredView
                {
                }
            }
            """);
        Assert.Empty(result.Errors);
        Assert.Contains("namespace ReefMarkupTest.Views;", result.GeneratedText);
    }

    [Fact]
    public void AccessIsInheritedFromCompanion()
    {
        MarkupCompilation result = MarkupTestHost.Compile("""
            <Reef xmlns="Reef" Class="InternalView" Namespace="ReefMarkupTest">
                <Element width="10" height="10" />
            </Reef>
            """, """
            namespace ReefMarkupTest
            {
                internal partial class InternalView
                {
                }
            }
            """);
        Assert.Empty(result.Errors);
        Assert.Contains("internal partial class InternalView", result.GeneratedText);
    }

    [Fact]
    public void ExplicitAccessIsUsed()
    {
        MarkupCompilation result = MarkupTestHost.Compile("""
            <Reef xmlns="Reef" Class="InternalView" Access="internal" Namespace="ReefMarkupTest">
                <Element width="10" height="10" />
            </Reef>
            """, """
            namespace ReefMarkupTest
            {
                public partial class InternalView
                {
                }
            }
            """);
        Assert.Contains("internal partial class InternalView", result.GeneratedText);
    }

    [Fact]
    public void ConcreteRendererDataClosesGenericMethod()
    {
        MarkupCompilation result = MarkupTestHost.Compile("""
            <Reef xmlns="Reef" Class="ConcreteView" Context="ViewContext" RendererData="Reef.PixelTexture" Namespace="ReefMarkupTest">
                <Image source="@context.Texture" width="10" height="10" />
            </Reef>
            """, """
            namespace ReefMarkupTest
            {
                public sealed class ViewContext
                {
                    public Reef.PixelTexture Texture { get; } = new(1, 1);
                }

                public partial class ConcreteView
                {
                }
            }
            """);
        //  The root has no Context, so @context is unresolved. The test only
        //  checks the concrete signature.
        Assert.DoesNotContain("Render<TData>", result.GeneratedText);
        Assert.Contains("UIBuilder<global::Reef.PixelTexture>", result.GeneratedText);
    }

    [Fact]
    public void MultipleRootsKeepDeclarationOrder()
    {
        MarkupCompilation result = MarkupTestHost.Compile("""
            <Reef xmlns="Reef" Class="MultiView" Namespace="ReefMarkupTest">
                <Element width="10" height="10" background="#ff0000" />
                <Element width="20" height="20" background="#00ff00" />
            </Reef>
            """, """
            namespace ReefMarkupTest
            {
                public partial class MultiView
                {
                }
            }
            """);
        Assert.Empty(result.Errors);
        int first = result.GeneratedText.IndexOf("Fixed(10)", StringComparison.Ordinal);
        int second = result.GeneratedText.IndexOf("Fixed(20)", StringComparison.Ordinal);
        Assert.True(first >= 0 && second > first);
    }
}
