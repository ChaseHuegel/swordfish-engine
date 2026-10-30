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
        Assert.Matches(
            @"var __param\d+ = new global::ReefMarkupTest\.ChildParameters \{ Value = ""Hello"" \};",
            result.GeneratedText);
        Assert.Contains("global::ReefMarkupTest.ChildParameters? parameters = null", result.GeneratedText);
        Assert.Matches(@"ui\.Text\(__param\d+\.Value\)", result.GeneratedText);
    }

    [Fact]
    public void DelegateParameterForwardsToInnerCallback()
    {
        MarkupCompilation result = MarkupTestHost.CompileFiles(
        [
            ("Stepper.rml", """
                <Reef xmlns="Reef" Class="Stepper" Parameters="StepperParameters" Namespace="ReefMarkupTest">
                    <Element onclick="@param.OnStep" width="100" height="20" />
                </Reef>
                """),
            ("View.rml", """
                <Reef xmlns="Reef"
                      xmlns:param="Reef.Param"
                      Class="View"
                      Namespace="ReefMarkupTest">
                    <Stepper param:onStep="OnStep" />
                </Reef>
                """),
        ], """
            using System;
            using Reef;
            using Reef.UI;

            namespace ReefMarkupTest
            {
                public readonly record struct StepperParameters(Action? OnStep = null);

                public partial class Stepper
                {
                }

                public partial class View
                {
                    public void OnStep()
                    {
                    }
                }
            }
            """);

        Assert.Empty(result.Errors);
        Assert.Contains("OnStep", result.GeneratedText);
        Assert.Contains("?.Invoke()", result.GeneratedText);
    }

    [Fact]
    public void NestedParameterizedComponentsCompileWithDistinctLocals()
    {
        MarkupCompilation result = MarkupTestHost.CompileFiles(
        [
            ("Inner.rml", """
                <Reef xmlns="Reef" Class="Inner" Parameters="InnerParameters" Namespace="ReefMarkupTest">
                    <Text text="@param.Label" />
                </Reef>
                """),
            ("Outer.rml", """
                <Reef xmlns="Reef"
                      xmlns:param="Reef.Param"
                      Class="Outer"
                      Parameters="OuterParameters"
                      Namespace="ReefMarkupTest">
                    <Inner param:label="@param.Title" />
                </Reef>
                """),
            ("ParentView.rml", """
                <Reef xmlns="Reef"
                      xmlns:param="Reef.Param"
                      Class="ParentView"
                      Namespace="ReefMarkupTest">
                    <Outer param:title="Nested" />
                </Reef>
                """),
        ], """
            namespace ReefMarkupTest
            {
                public readonly record struct InnerParameters(string Label = "");

                public readonly record struct OuterParameters(string Title = "");

                public partial class Inner
                {
                }

                public partial class Outer
                {
                }

                public partial class ParentView
                {
                }
            }
            """);

        Assert.Empty(result.Errors);
        Assert.Matches(@"var __param\d+ = new global::ReefMarkupTest\.InnerParameters \{ Label = __param\d+\.Title \};", result.GeneratedText);
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
