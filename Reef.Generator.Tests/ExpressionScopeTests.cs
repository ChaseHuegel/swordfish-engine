using Xunit;

namespace Reef.Generator.Tests;

public class ExpressionScopeTests
{
    private const string COMPANION = """
        using System;
        using System.Collections.Generic;

        namespace ReefMarkupTest
        {
            public sealed class Ctx
            {
                public List<string> Items { get; } = new();
            }

            public readonly record struct InnerParameters(string Label = "");

            public readonly record struct OuterParameters(
                string Title = "",
                string Id = "",
                Action<string, int>? Step = null);

            public partial class Inner
            {
            }

            public partial class Outer
            {
            }

            public partial class V
            {
                public void OnStep(string id, int delta)
                {
                }
            }
        }
        """;

    [Fact]
    public void NestedComponentCallDoesNotShadowTheCallerParameter()
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
                    <Element id="@(param.Id + '.after')" width="10" height="10" />
                </Reef>
                """),
            ("V.rml", """
                <Reef xmlns="Reef"
                      xmlns:param="Reef.Param"
                      Class="V"
                      Namespace="ReefMarkupTest">
                    <Outer param:title="Title" param:id="outer" />
                </Reef>
                """),
        ], COMPANION);

        Assert.Empty(result.Errors);
    }

    [Fact]
    public void CallSiteCallbackInsideAComponentUsesTheCallerParameter()
    {
        MarkupCompilation result = MarkupTestHost.CompileFiles(
        [
            ("Inner.rml", """
                <Reef xmlns="Reef" Class="Inner" Parameters="InnerParameters" Namespace="ReefMarkupTest">
                    <Element width="10" height="10" />
                </Reef>
                """),
            ("Outer.rml", """
                <Reef xmlns="Reef"
                      xmlns:param="Reef.Param"
                      Class="Outer"
                      Parameters="OuterParameters"
                      Namespace="ReefMarkupTest">
                    <Inner param:label="@param.Title" onclick="@param.Step(param.Id, -1)" />
                </Reef>
                """),
            ("V.rml", """
                <Reef xmlns="Reef"
                      xmlns:param="Reef.Param"
                      Class="V"
                      Namespace="ReefMarkupTest">
                    <Outer param:title="Title" param:id="outer" param:step="OnStep" />
                </Reef>
                """),
        ], COMPANION);

        Assert.True(result.Errors.Count == 0, result.GeneratedText);
    }

    [Fact]
    public void LoopVariablesDoNotLeakAfterTheLoop()
    {
        MarkupCompilation result = MarkupTestHost.Compile("""
            <Reef xmlns="Reef" Class="V" Context="Ctx" Namespace="ReefMarkupTest">
                <ForEach in="@context.Items" as="item">
                    <Text text="@item" />
                </ForEach>
                <Text text="@item" />
            </Reef>
            """, COMPANION);

        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "RF0004");
    }
}
