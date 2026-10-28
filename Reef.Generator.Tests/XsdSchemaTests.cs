using System.Runtime.CompilerServices;
using System.Xml;
using System.Xml.Schema;
using Xunit;

namespace Reef.Generator.Tests;

public class XsdSchemaTests
{
    private static readonly string SchemaPath = LocateSchema();

    [Fact]
    public void SchemaCompilesAndLoads()
    {
        XmlSchemaSet schemaSet = LoadSchema();

        Assert.True(schemaSet.IsCompiled);
        Assert.Equal(1, schemaSet.Count);
    }

    [Fact]
    public void ComprehensiveDocumentValidates()
    {
        const string markup = """
            <Reef xmlns="Reef"
                  xmlns:wb="Some.Namespace"
                  xmlns:class="Reef.Class"
                  xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
                  xsi:schemaLocation="Reef reef.xsd"
                  Class="HudView"
                  Access="public"
                  Context="HudState"
                  RendererData="Swordfish.Graphics.Material"
                  Namespace="ReefMarkupTest">
                <Element layout="vertical"
                         spacing="8"
                         width="100%"
                         height="fill"
                         padding="8 16"
                         corner-radius="4"
                         background="#101010"
                         border="1 #333333"
                         anchor="Top Right"
                         class="panel"
                         class:active="@context.IsActive"
                         visible="true">
                    <Text xml:space="preserve">Hello</Text>
                    <Text text="World"
                          size="18"
                          font="body"
                          text-align="center"
                          vertical-align="top"
                          wrap="false"
                          color="white" />
                    <Image source="@context.Texture" size="64 64" />
                    <If condition="@context.IsLoading">
                        <Text text="Loading..." />
                    </If>
                    <ElseIf condition="@context.HasError">
                        <Image source="@context.ErrorIcon" />
                    </ElseIf>
                    <Else>
                        <Text text="Ready." />
                    </Else>
                    <ForEach in="@context.Players" as="player" index="i">
                        <Element layout="horizontal" id="card_{player.Id}">
                            <Text text="{player.Name}" />
                        </Element>
                        <Empty>
                            <Text text="No players." />
                        </Empty>
                    </ForEach>
                    <Content name="header">
                        <Text text="Title" />
                    </Content>
                    <PlayerCard context="@context" width="240" />
                    <wb:Thing id="thing" />
                </Element>
            </Reef>
            """;

        ValidationOutcome outcome = Validate(markup);

        Assert.Null(outcome.XmlError);
        Assert.Empty(outcome.Errors);
    }

    [Fact]
    public void ClassDirectiveWithoutNamespaceFailsXmlParsing()
    {
        const string markup = """
            <Reef xmlns="Reef" Class="TestView">
                <Element class:active="@context.IsActive" />
            </Reef>
            """;

        ValidationOutcome outcome = Validate(markup);

        Assert.NotNull(outcome.XmlError);
        Assert.Empty(outcome.Errors);
    }

    [Fact]
    public void UnknownElementAttributeFailsValidation()
    {
        const string markup = """
            <Reef xmlns="Reef" Class="TestView">
                <Element unknown-attribute="value" />
            </Reef>
            """;

        ValidationOutcome outcome = Validate(markup);

        Assert.Null(outcome.XmlError);
        Assert.NotEmpty(outcome.Errors);
    }

    [Fact]
    public void ValuelessBooleanAttributeFailsXmlParsing()
    {
        const string markup = """
            <Reef xmlns="Reef" Class="TestView">
                <Element overlay />
            </Reef>
            """;

        ValidationOutcome outcome = Validate(markup);

        Assert.NotNull(outcome.XmlError);
    }

    [Fact]
    public void UnknownLayoutValueFailsValidation()
    {
        const string markup = """
            <Reef xmlns="Reef" Class="TestView">
                <Element layout="diagonal" />
            </Reef>
            """;

        ValidationOutcome outcome = Validate(markup);

        Assert.Null(outcome.XmlError);
        Assert.NotEmpty(outcome.Errors);
    }

    [Fact]
    public void TextWithChildElementFailsValidation()
    {
        const string markup = """
            <Reef xmlns="Reef" Class="TestView">
                <Text><Span /></Text>
            </Reef>
            """;

        ValidationOutcome outcome = Validate(markup);

        Assert.Null(outcome.XmlError);
        Assert.NotEmpty(outcome.Errors);
    }

    [Fact]
    public void ForEachWithTwoEmptyTagsFailsValidation()
    {
        const string markup = """
            <Reef xmlns="Reef" Class="TestView">
                <ForEach in="@context.Items" as="item">
                    <Text text="Item" />
                    <Empty />
                    <Empty />
                </ForEach>
            </Reef>
            """;

        ValidationOutcome outcome = Validate(markup);

        Assert.Null(outcome.XmlError);
        Assert.NotEmpty(outcome.Errors);
    }

    [Fact]
    public void NamedContentValidates()
    {
        const string markup = """
            <Reef xmlns="Reef" Class="ModalDialog">
                <Element layout="vertical">
                    <Content name="header">
                        <Text text="Default Title" />
                    </Content>
                </Element>
            </Reef>
            """;

        ValidationOutcome outcome = Validate(markup);

        Assert.Null(outcome.XmlError);
        Assert.Empty(outcome.Errors);
    }

    [Fact]
    public void ImageExpressionSourceValidates()
    {
        const string markup = """
            <Reef xmlns="Reef" Class="ImageView" Context="ImageState">
                <Element layout="none">
                    <Image source="@context.Texture" />
                </Element>
            </Reef>
            """;

        ValidationOutcome outcome = Validate(markup);

        Assert.Null(outcome.XmlError);
        Assert.Empty(outcome.Errors);
    }

    private static ValidationOutcome Validate(string markup)
    {
        XmlSchemaSet schemaSet = LoadSchema();
        var settings = new XmlReaderSettings
        {
            ValidationType = ValidationType.Schema,
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            Schemas = schemaSet,
        };

        var errors = new List<string>();
        settings.ValidationEventHandler += (_, args) =>
        {
            if (args.Severity == XmlSeverityType.Error)
            {
                errors.Add(args.Message);
            }
        };

        try
        {
            using var stringReader = new StringReader(markup);
            using XmlReader reader = XmlReader.Create(stringReader, settings);
            while (reader.Read())
            {
            }
        }
        catch (XmlException exception)
        {
            return new ValidationOutcome(errors, exception);
        }

        return new ValidationOutcome(errors, null);
    }

    private static XmlSchemaSet LoadSchema()
    {
        var schemaSet = new XmlSchemaSet { XmlResolver = null };
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
        };

        using XmlReader reader = XmlReader.Create(SchemaPath, settings);
        schemaSet.Add(null, reader);
        schemaSet.Compile();
        return schemaSet;
    }

    private static string LocateSchema([CallerFilePath] string callerFilePath = "")
    {
        DirectoryInfo? directory = new(Path.GetDirectoryName(callerFilePath)!);
        while (directory != null)
        {
            string candidate = Path.Combine(directory.FullName, "Reef.Generator", "Schemas", "reef.xsd");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Could not locate Reef.Generator/Schemas/reef.xsd.", callerFilePath);
    }

    private sealed record ValidationOutcome(IReadOnlyList<string> Errors, XmlException? XmlError);
}
