using System.Runtime.CompilerServices;
using System.Xml;
using System.Xml.Schema;
using Xunit;

namespace Reef.Generator.Tests;

public class FixtureSchemaTests
{
    [Fact]
    public void AllMarkupFixturesValidateAgainstSchema()
    {
        string repoRoot = RepoRoot();
        XmlSchemaSet schemas = LoadSchema(repoRoot);
        string[] fixtures = Directory
            .GetFiles(Path.Combine(repoRoot, "Reef.Markup.Tests", "Fixtures"), "*.rml")
            .Concat(Directory.GetFiles(Path.Combine(repoRoot, "Reef.Markup.TestComponents", "Components"), "*.rml"))
            .ToArray();

        Assert.NotEmpty(fixtures);

        foreach (string fixture in fixtures)
        {
            var errors = new List<string>();
            var settings = new XmlReaderSettings
            {
                ValidationType = ValidationType.Schema,
                Schemas = schemas,
            };
            settings.ValidationEventHandler += (_, args) => errors.Add(args.Message);

            using var reader = XmlReader.Create(fixture, settings);
            try
            {
                while (reader.Read())
                {
                }
            }
            catch (XmlException exception)
            {
                errors.Add(exception.Message);
            }

            Assert.True(errors.Count == 0, fixture + ": " + string.Join(" | ", errors));
        }
    }

    private static XmlSchemaSet LoadSchema(string repoRoot)
    {
        var schemas = new XmlSchemaSet();
        schemas.Add("Reef", Path.Combine(repoRoot, "Reef.Generator", "Schemas", "reef.xsd"));
        return schemas;
    }

    private static string RepoRoot([CallerFilePath] string path = "")
    {
        return Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path)!, ".."));
    }
}

public class SchemaVocabularyTests
{
    [Fact]
    public void SchemaCoversMarkupVocabulary()
    {
        string repoRoot = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ThisFile())!, ".."));
        var document = System.Xml.Linq.XDocument.Load(Path.Combine(repoRoot, "Reef.Generator", "Schemas", "reef.xsd"));
        var elements = new HashSet<string>(document.Descendants().Where(node => node.Name.LocalName == "element").Select(node => (string?)node.Attribute("name")).Where(name => name != null)!, StringComparer.Ordinal);
        var attributes = new HashSet<string>(document.Descendants().Where(node => node.Name.LocalName == "attribute").Select(node => (string?)node.Attribute("name")).Where(name => name != null)!, StringComparer.Ordinal);

        foreach (string tag in new[] { "Element", "Text", "Image", "If", "ElseIf", "Else", "ForEach", "Empty", "Content" })
        {
            Assert.Contains(tag, elements);
        }

        foreach (string attribute in MarkupVocabulary.COMMON_ATTRIBUTES
                     .Concat(MarkupVocabulary.ELEMENT_ATTRIBUTES)
                     .Concat(MarkupVocabulary.TEXT_ATTRIBUTES)
                     .Concat(MarkupVocabulary.IMAGE_ATTRIBUTES))
        {
            Assert.Contains(attribute, attributes);
        }
    }

    private static string ThisFile([System.Runtime.CompilerServices.CallerFilePath] string path = "")
    {
        return path;
    }
}
