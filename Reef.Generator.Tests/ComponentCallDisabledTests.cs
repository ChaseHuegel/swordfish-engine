using Xunit;

namespace Reef.Generator.Tests;

public class ComponentCallDisabledTests
{
    [Fact]
    public void LocalComponentCallDisabledGuardsCallbacks()
    {
        MarkupCompilation result = MarkupTestHost.CompileFiles(
        [
            ("Child.rml", """
                <Reef xmlns="Reef" Class="Child" Namespace="ReefMarkupTest">
                    <Element>
                        <Content />
                    </Element>
                </Reef>
                """),
            ("ParentView.rml", """
                <Reef xmlns="Reef"
                      Class="ParentView"
                      Context="ParentState"
                      Namespace="ReefMarkupTest">
                    <Child disabled="@context.Disabled" onclick="OnChild" />
                </Reef>
                """),
        ], """
            namespace ReefMarkupTest
            {
                public sealed class ParentState
                {
                    public bool Disabled { get; set; }
                }

                public partial class Child
                {
                }

                public partial class ParentView
                {
                    partial void OnChild();
                }
            }
            """);

        Assert.Empty(result.Errors);
        Assert.Contains("OnChild", result.GeneratedText);
        Assert.Contains("!context.Disabled", result.GeneratedText);
    }
}
