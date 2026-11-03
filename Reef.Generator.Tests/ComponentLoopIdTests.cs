using Xunit;

namespace Reef.Generator.Tests;

public class ComponentLoopIdTests
{
    private const string COMPANION = """
        using System.Collections.Generic;

        namespace ReefMarkupTest
        {
            public sealed class Ctx
            {
                public List<string> Items { get; } = new();
            }

            public readonly record struct ChildParameters(System.Action? OnClick = null);

            public partial class Child
            {
            }

            public partial class V
            {
                public void OnClick()
                {
                }
            }
        }
        """;

    private static MarkupCompilation Compile(string childIdAttribute)
    {
        return MarkupTestHost.CompileFiles(
        [
            ("Child.rml", """
                <Reef xmlns="Reef" Class="Child" Parameters="ChildParameters" Namespace="ReefMarkupTest">
                    <Element onclick="@param.OnClick" width="20" height="20" />
                </Reef>
                """),
            ("V.rml", $$"""
                <Reef xmlns="Reef"
                      xmlns:param="Reef.Param"
                      Class="V"
                      Context="Ctx"
                      Namespace="ReefMarkupTest">
                    <ForEach in="@context.Items" as="item" index="i">
                        <Child {{childIdAttribute}} param:onClick="OnClick" />
                    </ForEach>
                </Reef>
                """),
        ], COMPANION);
    }

    [Fact]
    public void ComponentCallInLoopAcceptsTheCallSiteKeyBoundId()
    {
        MarkupCompilation result = Compile("id=\"child_{i}\"");

        Assert.Empty(result.Errors);
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Id == "RF0010");
    }

    [Fact]
    public void ComponentCallInLoopWithAStaticIdReportsRf0010()
    {
        MarkupCompilation result = Compile("id=\"child\"");

        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "RF0010");
    }

    [Fact]
    public void ComponentBodyLoopMayDeriveIdsFromItsParameters()
    {
        MarkupCompilation result = MarkupTestHost.CompileFiles(
        [
            ("Child.rml", """
                <Reef xmlns="Reef" Class="Child" Parameters="ChildParameters" Namespace="ReefMarkupTest">
                    <Element width="200" height="20">
                        <ForEach in="@param.Options" as="option" index="i">
                            <Element class="cell" id="@(param.Id + '.' + i)" width="10" height="10" />
                        </ForEach>
                    </Element>
                </Reef>
                """),
            ("V.rml", """
                <Reef xmlns="Reef"
                      xmlns:param="Reef.Param"
                      Class="V"
                      Context="Ctx"
                      Namespace="ReefMarkupTest">
                    <ForEach in="@context.Items" as="item" index="i">
                        <Child param:id="@item" param:options="@context.Items" />
                    </ForEach>
                </Reef>
                """),
            ("Theme.rss", ".cell { color: #000000; } .cell:hover { color: #ffffff; }"),
        ], """
            using System.Collections.Generic;

            namespace ReefMarkupTest
            {
                public sealed class Ctx
                {
                    public List<string> Items { get; } = new();
                }

                public readonly record struct ChildParameters(string Id = "", string[] Options = null!);

                public partial class Child
                {
                }

                public partial class V
                {
                }
            }
            """);

        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Id == "RF0010");
    }
}
