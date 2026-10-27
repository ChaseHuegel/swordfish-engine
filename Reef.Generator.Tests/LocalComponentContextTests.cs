using Xunit;

namespace Reef.Generator.Tests;

public class LocalComponentContextTests
{
    [Fact]
    public void LocalComponentUsesChildContext()
    {
        MarkupCompilation result = MarkupTestHost.CompileFiles(
        [
            ("Child.rml", """
                <Reef xmlns="Reef" Class="Child" Context="ChildState" Namespace="ReefMarkupTest">
                    <Text text="@context.Value" />
                </Reef>
                """),
            ("ParentView.rml", """
                <Reef xmlns="Reef" Class="ParentView" Context="ParentState" Namespace="ReefMarkupTest">
                    <Element layout="vertical">
                        <Child context="@context.Child" />
                    </Element>
                </Reef>
                """),
        ], """
            namespace ReefMarkupTest
            {
                public sealed class ParentState
                {
                    public ChildState Child { get; set; } = new();
                }

                public sealed class ChildState
                {
                    public string Value { get; set; } = string.Empty;
                }

                public partial class Child
                {
                }

                public partial class ParentView
                {
                }
            }
            """);

        Assert.Empty(result.Errors);
        Assert.Contains("context.Child.Value", result.GeneratedText);
    }

    [Fact]
    public void NestedLocalComponentUsesNestedContext()
    {
        MarkupCompilation result = MarkupTestHost.CompileFiles(
        [
            ("ScrollBar.rml", """
                <Reef xmlns="Reef" Class="ScrollBar" Context="ScrollBarState" Namespace="ReefMarkupTest">
                    <Element height="@context.ThumbHeight" />
                </Reef>
                """),
            ("ScrollArea.rml", """
                <Reef xmlns="Reef" Class="ScrollArea" Context="ScrollAreaState" Namespace="ReefMarkupTest">
                    <Element layout="vertical">
                        <ScrollBar context="@context.Bar" />
                    </Element>
                </Reef>
                """),
            ("ParentView.rml", """
                <Reef xmlns="Reef" Class="ParentView" Context="ParentState" Namespace="ReefMarkupTest">
                    <ScrollArea context="@context.Scroll" />
                </Reef>
                """),
        ], """
            namespace ReefMarkupTest
            {
                public sealed class ParentState
                {
                    public ScrollAreaState Scroll { get; set; } = new();
                }

                public sealed class ScrollAreaState
                {
                    public ScrollBarState Bar { get; set; } = new();
                }

                public sealed class ScrollBarState
                {
                    public int ThumbHeight { get; set; } = 1;
                }

                public partial class ScrollArea
                {
                }

                public partial class ScrollBar
                {
                }

                public partial class ParentView
                {
                }
            }
            """);

        Assert.Empty(result.Errors);
        Assert.Contains("context.Scroll.Bar.ThumbHeight", result.GeneratedText);
    }
}
