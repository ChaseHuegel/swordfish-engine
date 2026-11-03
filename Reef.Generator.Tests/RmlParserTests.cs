using Microsoft.CodeAnalysis;
using Xunit;

namespace Reef.Generator.Tests;

public class RmlParserTests
{
    private static RmlDocument Parse(string markup)
    {
        return new RmlParser(new AdditionalFileItem("Test.rml", markup)).Parse(CancellationToken.None);
    }

    [Fact]
    public void RootAttributesAndMultipleRootsAreRead()
    {
        RmlDocument document = Parse("""
            <Reef xmlns="Reef"
                  xmlns:wb="Some.Widgets"
                  Class="TestView"
                  Access="internal"
                  Context="TestContext"
                  RendererData="Reef.PixelTexture"
                  Namespace="Custom.Space">
                <Element />
                <Text text="x" />
            </Reef>
            """);
        Assert.Equal("TestView", document.ClassName);
        Assert.Equal("internal", document.Access);
        Assert.Equal("TestContext", document.ContextType);
        Assert.Equal("Reef.PixelTexture", document.RendererDataType);
        Assert.Equal("Custom.Space", document.NamespaceName);
        Assert.Equal(2, document.Roots.Count);
        Assert.Empty(document.Diagnostics);
    }

    [Fact]
    public void CommentsAreIgnored()
    {
        RmlDocument document = Parse("""
            <Reef xmlns="Reef" Class="V">
                <!-- before -->
                <Element>
                    <!-- inside -->
                </Element>
                <!-- after -->
            </Reef>
            """);
        Assert.Single(document.Roots);
        Assert.Empty(document.Diagnostics);
    }

    [Fact]
    public void TextBodyUsesAttributeFirstAndTrimsByDefault()
    {
        RmlDocument document = Parse("""
            <Reef xmlns="Reef" Class="V">
                <Text text="Attr">
                    Body
                </Text>
                <Text>
                    Trimmed
                </Text>
                <Text xml:space="preserve">
                    Preserved
                </Text>
            </Reef>
            """);
        var first = (RmlElementNode)document.Roots[0];
        var second = (RmlElementNode)document.Roots[1];
        var third = (RmlElementNode)document.Roots[2];
        Assert.Equal("Attr", first.GetAttributeText("text"));
        Assert.Contains("Body", first.TextContent);
        Assert.Equal("Trimmed", second.TextContent!.Trim());
        Assert.True(third.PreserveWhitespace);
        Assert.Contains("Preserved", third.TextContent);
    }

    [Fact]
    public void ConditionsAndEmptyAreGrouped()
    {
        RmlDocument document = Parse("""
            <Reef xmlns="Reef" Class="V">
                <If condition="true"><Text text="a" /></If>
                <ElseIf condition="false"><Text text="b" /></ElseIf>
                <Else><Text text="c" /></Else>
                <ForEach in="@context.Items" as="item" index="i">
                    <Text text="x" />
                    <Empty><Text text="empty" /></Empty>
                </ForEach>
            </Reef>
            """);
        Assert.IsType<RmlConditionalNode>(document.Roots[0]);
        var conditional = (RmlConditionalNode)document.Roots[0];
        Assert.Equal(3, conditional.Branches.Count);
        Assert.Null(conditional.Branches[2].Condition);

        var forEach = Assert.IsType<RmlForEachNode>(document.Roots[1]);
        Assert.Single(forEach.Children);
        Assert.Single(forEach.EmptyChildren);
    }

    [Fact]
    public void ValuelessAttributeReportsRf0001()
    {
        RmlDocument document = Parse("""
            <Reef xmlns="Reef" Class="V">
                <Element overlay />
            </Reef>
            """);
        Assert.Contains(document.Diagnostics, diagnostic => diagnostic.Id == "RF0001");
    }

    [Fact]
    public void MissingClassReportsRf0026()
    {
        RmlDocument document = Parse("""
            <Reef xmlns="Reef">
                <Element />
            </Reef>
            """);
        Assert.Contains(document.Diagnostics, diagnostic => diagnostic.Id == "RF0026");
    }

    [Fact]
    public void TextChildReportsRf0031()
    {
        RmlDocument document = Parse("""
            <Reef xmlns="Reef" Class="V">
                <Text><Span /></Text>
            </Reef>
            """);
        Assert.Contains(document.Diagnostics, diagnostic => diagnostic.Id == "RF0031");
    }

    [Fact]
    public void UnknownRootAttributeReportsRf0019()
    {
        RmlDocument document = Parse("""
            <Reef xmlns="Reef" Class="V" Unknown="x">
                <Element />
            </Reef>
            """);
        Assert.Contains(document.Diagnostics, diagnostic => diagnostic.Id == "RF0019");
    }

    [Fact]
    public void MissingIfConditionReportsRf0026()
    {
        RmlDocument document = Parse("""
            <Reef xmlns="Reef" Class="V">
                <If><Text text="x" /></If>
            </Reef>
            """);
        Assert.Contains(document.Diagnostics, diagnostic => diagnostic.Id == "RF0026");
    }

    [Fact]
    public void WrongRootReportsRf0001()
    {
        RmlDocument document = Parse("""
            <NotReef xmlns="Reef" Class="V" />
            """);
        Assert.Contains(document.Diagnostics, diagnostic => diagnostic.Id == "RF0001");
    }
}
