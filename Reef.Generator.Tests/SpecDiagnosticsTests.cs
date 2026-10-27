using Microsoft.CodeAnalysis;
using Xunit;

namespace Reef.Generator.Tests;

public class SpecDiagnosticsTests
{
    private const string COMPANION = """
        using System.Collections.Generic;
        using Reef;
        using Reef.UI;

        namespace ReefMarkupTest
        {
            public sealed class Ctx
            {
                public string Name { get; set; } = string.Empty;
                public string ReadOnlyName => Name;
                public List<string> Items { get; } = new();
            }
        }

        namespace Test.Widgets
        {
            public static class Widgets
            {
                public static bool Checkbox(this UIBuilder<Reef.PixelTexture> ui, string id, string text, bool value) => value;
                public static void Label(this UIBuilder<Reef.PixelTexture> ui, string text) { }
            }
        }
        """;

    private static MarkupCompilation Compile(string markup, string? rss = null)
    {
        return MarkupTestHost.Compile(markup, COMPANION, rss);
    }

    private static void AssertDiagnostic(MarkupCompilation result, string id)
    {
        Assert.True(
            result.Diagnostics.Any(diagnostic => diagnostic.Id == id),
            "Expected " + id + ". Diagnostics: " + string.Join(", ", result.Diagnostics.Select(diagnostic => diagnostic.Id)));
    }

    [Fact]
    public void XmlSyntaxErrorReportsRf0001()
    {
        AssertDiagnostic(Compile("""
            <Reef xmlns="Reef" Class="V">
                <Element>
            </Reef>
            """), "RF0001");
    }

    [Fact]
    public void UndefinedVariableReportsRf0002()
    {
        AssertDiagnostic(Compile("""
            <Reef xmlns="Reef" Class="V">
                <Element class="a" width="10" />
            </Reef>
            """, ".a { color: var(--missing); }"), "RF0002");
    }

    [Fact]
    public void CircularInheritanceReportsRf0003()
    {
        AssertDiagnostic(Compile("""
            <Reef xmlns="Reef" Class="V">
                <Element class="a" width="10" />
            </Reef>
            """, ".a : .b { color: red; } .b : .a { color: blue; }"), "RF0003");
    }

    [Fact]
    public void UnresolvedContextMemberReportsRf0004()
    {
        AssertDiagnostic(Compile("""
            <Reef xmlns="Reef" Class="V" Context="Ctx">
                <Text text="@context.Missing" />
            </Reef>
            """), "RF0004");
    }


    [Fact]
    public void UnusedContentSlotReportsRf0006()
    {
        MarkupCompilation result = MarkupTestHost.CompileFiles(
        [
            ("V.rml", """
                <Reef xmlns="Reef" Class="V" Namespace="ReefMarkupTest">
                    <Element>
                        <Card>
                            <Content name="junk"><Text text="x" /></Content>
                        </Card>
                    </Element>
                </Reef>
                """),
            ("Card.rml", """
                <Reef xmlns="Reef" Class="Card" Namespace="ReefMarkupTest">
                    <Element><Content name="header"><Text text="header" /></Content></Element>
                </Reef>
                """),
        ], COMPANION);
        AssertDiagnostic(result, "RF0006");
    }

    [Fact]
    public void InvalidConstraintReportsRf0005()
    {
        AssertDiagnostic(Compile("""
            <Reef xmlns="Reef" Class="V">
                <Element width="banana" />
            </Reef>
            """), "RF0005");
    }

    [Fact]
    public void UnresolvedNamespaceReportsRf0007()
    {
        AssertDiagnostic(Compile("""
            <Reef xmlns="Reef" xmlns:wb="No.Such.Namespace" Class="V">
                <wb:Thing />
            </Reef>
            """), "RF0007");
    }

    [Fact]
    public void MisplacedElseIfReportsRf0008()
    {
        AssertDiagnostic(Compile("""
            <Reef xmlns="Reef" Class="V">
                <ElseIf condition="true"><Text text="x" /></ElseIf>
            </Reef>
            """), "RF0008");
    }

    [Fact]
    public void MisplacedEmptyReportsRf0009()
    {
        AssertDiagnostic(Compile("""
            <Reef xmlns="Reef" Class="V">
                <Empty><Text text="x" /></Empty>
            </Reef>
            """), "RF0009");
    }

    [Fact]
    public void LoopInteractionWithoutKeyReportsRf0010()
    {
        AssertDiagnostic(Compile("""
            <Reef xmlns="Reef" Class="V" Context="Ctx">
                <ForEach in="@context.Items" as="item">
                    <Element onclick="OnClick"><Text text="x" /></Element>
                </ForEach>
            </Reef>
            """), "RF0010");
    }

    [Fact]
    public void StaticLoopIdReportsRf0010()
    {
        AssertDiagnostic(Compile("""
            <Reef xmlns="Reef" Class="V" Context="Ctx">
                <ForEach in="@context.Items" as="item">
                    <Element id="same" onclick="OnClick"><Text text="x" /></Element>
                </ForEach>
            </Reef>
            """), "RF0010");
    }

    [Fact]
    public void ReadOnlyBindTargetReportsRf0011()
    {
        AssertDiagnostic(Compile("""
            <Reef xmlns="Reef" xmlns:wb="Test.Widgets" Class="V" Context="Ctx" RendererData="Reef.PixelTexture">
                <wb:Checkbox id="cb" text="x" value="@context.ReadOnlyName" bind="context.ReadOnlyName" />
            </Reef>
            """), "RF0011");
    }

    [Fact]
    public void StructContextBindsToViewField()
    {
        MarkupCompilation result = MarkupTestHost.Compile("""
            <Reef xmlns="Reef" xmlns:wb="Test.BindWidgets" Class="V" Context="Ctx" RendererData="Reef.PixelTexture">
                <wb:TextInput id="t" bind="State" />
            </Reef>
            """, BIND_COMPANION);

        Assert.Empty(result.Errors);
        Assert.Contains("ref State", result.GeneratedText);
    }

    [Fact]
    public void StructContextBindToContextReportsRf0011()
    {
        MarkupCompilation result = MarkupTestHost.Compile("""
            <Reef xmlns="Reef" xmlns:wb="Test.BindWidgets" Class="V" Context="Ctx" RendererData="Reef.PixelTexture">
                <wb:TextInput id="t" bind="context.Name" />
            </Reef>
            """, BIND_COMPANION);

        AssertDiagnostic(result, "RF0011");
    }

    private const string BIND_COMPANION = """
        using Reef;
        using Reef.UI;

        namespace ReefMarkupTest
        {
            public struct Ctx
            {
                public string Name { get; set; }
            }

            public struct BoxState
            {
                public string Text;
            }

            public partial class V
            {
                public BoxState State;
            }
        }

        namespace Test.BindWidgets
        {
            public static class Widgets
            {
                public static void TextInput(this UIBuilder<Reef.PixelTexture> ui, string id, ref ReefMarkupTest.BoxState state) { }
            }
        }
        """;

    [Fact]
    public void DuplicateVariableReportsRf0012()
    {
        MarkupCompilation result = MarkupTestHost.CompileFiles(
        [
            ("V.rml", """
                <Reef xmlns="Reef" Class="V">
                    <Element class="a" width="10" />
                </Reef>
                """),
            ("A.rss", ":root { --x: #ff0000; } .a { color: var(--x); }"),
            ("B.rss", ":root { --x: #00ff00; }"),
        ], COMPANION);
        AssertDiagnostic(result, "RF0012");
    }

    [Fact]
    public void DuplicateOverrideReportsRf0013()
    {
        MarkupCompilation result = MarkupTestHost.CompileFiles(
        [
            ("V.rml", """
                <Reef xmlns="Reef" Class="V">
                    <Element class="a" width="10" />
                </Reef>
                """),
            ("A.rss", ":root { override --x: #ff0000; } .a { color: var(--x); }"),
            ("B.rss", ":root { override --x: #00ff00; }"),
        ], COMPANION);
        AssertDiagnostic(result, "RF0013");
    }

    [Fact]
    public void VoidWidgetWithChildrenReportsRf0014()
    {
        AssertDiagnostic(Compile("""
            <Reef xmlns="Reef" xmlns:wb="Test.Widgets" Class="V" RendererData="Reef.PixelTexture">
                <wb:Label text="x"><Text text="child" /></wb:Label>
            </Reef>
            """), "RF0014");
    }

    [Fact]
    public void UnknownElementReportsRf0015()
    {
        AssertDiagnostic(Compile("""
            <Reef xmlns="Reef" Class="V">
                <Nope />
            </Reef>
            """), "RF0015");
    }

    [Fact]
    public void InvalidColorReportsRf0016()
    {
        AssertDiagnostic(Compile("""
            <Reef xmlns="Reef" Class="V">
                <Element background="#ggg" />
            </Reef>
            """), "RF0016");
    }

    [Fact]
    public void InvalidFontSizeReportsRf0017()
    {
        AssertDiagnostic(Compile("""
            <Reef xmlns="Reef" Class="V">
                <Text text="x" font-size="large" />
            </Reef>
            """), "RF0017");
    }

    [Fact]
    public void InvalidShorthandReportsRf0018()
    {
        AssertDiagnostic(Compile("""
            <Reef xmlns="Reef" Class="V">
                <Element padding="1 2 3" />
            </Reef>
            """), "RF0018");
    }

    [Fact]
    public void UnknownAttributeReportsRf0019()
    {
        AssertDiagnostic(Compile("""
            <Reef xmlns="Reef" Class="V">
                <Element banana="1" />
            </Reef>
            """), "RF0019");
    }

    [Fact]
    public void InvalidInterpolationReportsRf0020()
    {
        AssertDiagnostic(Compile("""
            <Reef xmlns="Reef" Class="V">
                <Text text="hello {context." />
            </Reef>
            """), "RF0020");
    }

    [Fact]
    public void TypeMismatchReportsRf0021()
    {
        AssertDiagnostic(Compile("""
            <Reef xmlns="Reef" Class="V" Context="Ctx">
                <Element width="@context.Name" />
            </Reef>
            """), "RF0021");
    }

    [Fact]
    public void UnknownStylePropertyReportsRf0022()
    {
        AssertDiagnostic(Compile("""
            <Reef xmlns="Reef" Class="V">
                <Element class="a" width="10" />
            </Reef>
            """, ".a { wat: 1; }"), "RF0022");
    }

    [Fact]
    public void InvalidEnumReportsRf0023()
    {
        AssertDiagnostic(Compile("""
            <Reef xmlns="Reef" Class="V">
                <Element layout="diagonal" />
            </Reef>
            """), "RF0023");
    }

    [Fact]
    public void MissingRequiredAttributeReportsRf0026()
    {
        AssertDiagnostic(Compile("""
            <Reef xmlns="Reef" Class="V" RendererData="Reef.PixelTexture">
                <Image />
            </Reef>
            """), "RF0026");
    }

    [Fact]
    public void InvalidRootReportsRf0028()
    {
        AssertDiagnostic(Compile("""
            <Reef xmlns="Reef" Class="123" />
            """), "RF0028");
    }

    [Fact]
    public void UnsupportedImageWithoutRendererDataReportsRf0031()
    {
        AssertDiagnostic(Compile("""
            <Reef xmlns="Reef" Class="V" Context="Ctx">
                <Image source="@context.Name" />
            </Reef>
            """), "RF0031");
    }

    [Fact]
    public void UnknownStyleClassReportsRf0032()
    {
        AssertDiagnostic(Compile("""
            <Reef xmlns="Reef" Class="V">
                <Element class="nope" width="10" />
            </Reef>
            """), "RF0032");
    }


    [Fact]
    public void DuplicateIdReportsRf0024()
    {
        AssertDiagnostic(Compile("""
            <Reef xmlns="Reef" Class="V">
                <Element id="same" width="10" height="10" />
                <Element id="same" width="10" height="10" />
            </Reef>
            """), "RF0024");
    }

    [Fact]
    public void InvalidClassDirectiveReportsRf0027()
    {
        AssertDiagnostic(Compile("""
            <Reef xmlns="Reef" xmlns:class="Reef.Class" Class="V">
                <Element class:bad.name="true" width="10" />
            </Reef>
            """), "RF0027");
    }

    [Fact]
    public void CircularComponentReportsRf0025()
    {
        MarkupCompilation result = MarkupTestHost.CompileFiles(
        [
            ("V.rml", """
                <Reef xmlns="Reef" Class="V" Namespace="ReefMarkupTest">
                    <Element><A /></Element>
                </Reef>
                """),
            ("A.rml", """
                <Reef xmlns="Reef" Class="A" Namespace="ReefMarkupTest">
                    <Element><B /></Element>
                </Reef>
                """),
            ("B.rml", """
                <Reef xmlns="Reef" Class="B" Namespace="ReefMarkupTest">
                    <Element><A /></Element>
                </Reef>
                """),
        ], COMPANION);
        AssertDiagnostic(result, "RF0025");
    }

    [Fact]
    public void DuplicateComponentClassReportsRf0029()
    {
        MarkupCompilation result = MarkupTestHost.CompileFiles(
        [
            ("A.rml", """
                <Reef xmlns="Reef" Class="Dup" Namespace="ReefMarkupTest">
                    <Element><Text text="A" /></Element>
                </Reef>
                """),
            ("B.rml", """
                <Reef xmlns="Reef" Class="Dup" Namespace="ReefMarkupTest">
                    <Element><Text text="B" /></Element>
                </Reef>
                """),
        ], COMPANION);
        AssertDiagnostic(result, "RF0029");
    }

    [Fact]
    public void ComponentContextMismatchReportsRf0030()
    {
        MarkupCompilation result = MarkupTestHost.CompileFiles(
        [
            ("V.rml", """
                <Reef xmlns="Reef" Class="V" Context="Ctx" Namespace="ReefMarkupTest">
                    <Element><Card /></Element>
                </Reef>
                """),
            ("Card.rml", """
                <Reef xmlns="Reef" Class="Card" Context="Ctx" Namespace="ReefMarkupTest">
                    <Element><Text text="@context.Name" /></Element>
                </Reef>
                """),
        ], COMPANION);
        AssertDiagnostic(result, "RF0030");
    }

    [Fact]
    public void ScrollCallbackWithoutModeReportsRf0033()
    {
        AssertDiagnostic(Compile("""
            <Reef xmlns="Reef" Class="V">
                <Element onscroll="OnScroll" width="10" height="10" />
            </Reef>
            """), "RF0033");
    }

    [Fact]
    public void RF0034_UndefinedBaseClass()
    {
        AssertDiagnostic(Compile("""
            <Reef xmlns="Reef" Class="V">
                <Element class="child" width="10" height="10" />
            </Reef>
            """, ".child { --extends: nonExistentBase; }"), "RF0034");
    }
}
