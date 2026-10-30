using Xunit;

namespace Reef.Generator.Tests;

public class ComponentParameterTests
{
    [Fact]
    public void LocalComponentReceivesTypedParameters()
    {
        MarkupCompilation result = MarkupTestHost.CompileFiles(
        [
            ("Child.rml", """
                <Reef xmlns="Reef" Class="Child" Parameters="ChildParameters" Namespace="ReefMarkupTest">
                    <Text text="@param.Value" />
                </Reef>
                """),
            ("ParentView.rml", """
                <Reef xmlns="Reef"
                      xmlns:param="Reef.Param"
                      Class="ParentView"
                      Context="ParentState"
                      Namespace="ReefMarkupTest">
                    <Child param:value="Hello" />
                </Reef>
                """),
        ], """
            namespace ReefMarkupTest
            {
                public sealed class ParentState
                {
                }

                public readonly record struct ChildParameters(string Value = "Default");

                public partial class Child
                {
                }

                public partial class ParentView
                {
                }
            }
            """);

        Assert.Empty(result.Errors);
        Assert.Contains("var param = new global::ReefMarkupTest.ChildParameters { Value = \"Hello\" };", result.GeneratedText);
        Assert.Contains("global::ReefMarkupTest.ChildParameters? parameters = null", result.GeneratedText);
        Assert.Contains("ui.Text(param.Value)", result.GeneratedText);
    }

    [Fact]
    public void LocalComponentUsesParameterDefaults()
    {
        MarkupCompilation result = MarkupTestHost.CompileFiles(
        [
            ("Child.rml", """
                <Reef xmlns="Reef" Class="Child" Parameters="ChildParameters" Namespace="ReefMarkupTest">
                    <Text text="@param.Value" />
                </Reef>
                """),
            ("ParentView.rml", """
                <Reef xmlns="Reef" Class="ParentView" Namespace="ReefMarkupTest">
                    <Child />
                </Reef>
                """),
        ], """
            namespace ReefMarkupTest
            {
                public readonly record struct ChildParameters(string Value = "Default");

                public partial class Child
                {
                }

                public partial class ParentView
                {
                }
            }
            """);

        Assert.Empty(result.Errors);
        Assert.Contains("var param = parameters ?? new global::ReefMarkupTest.ChildParameters();", result.GeneratedText);
    }

    [Fact]
    public void UnknownParameterReportsDiagnostic()
    {
        MarkupCompilation result = MarkupTestHost.CompileFiles(
        [
            ("Child.rml", """
                <Reef xmlns="Reef" Class="Child" Parameters="ChildParameters" Namespace="ReefMarkupTest">
                    <Text text="@param.Value" />
                </Reef>
                """),
            ("ParentView.rml", """
                <Reef xmlns="Reef"
                      xmlns:param="Reef.Param"
                      Class="ParentView"
                      Namespace="ReefMarkupTest">
                    <Child param:missing="Hello" />
                </Reef>
                """),
        ], """
            namespace ReefMarkupTest
            {
                public readonly record struct ChildParameters(string Value = "Default");

                public partial class Child
                {
                }

                public partial class ParentView
                {
                }
            }
            """);

        Assert.NotEmpty(result.Errors);
    }
}
