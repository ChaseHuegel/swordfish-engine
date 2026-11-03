using Reef;
using Xunit;

namespace Reef.Generator.Tests;

public class AotContractTests
{
    [Fact]
    public void GeneratedCodeHasNoRuntimeParsingOrReflection()
    {
        MarkupCompilation result = MarkupTestHost.Compile("""
            <Reef xmlns="Reef" Class="V">
                <Element><Text text="Hello" /></Element>
            </Reef>
            """, """
            namespace ReefMarkupTest
            {
                public partial class V
                {
                }
            }
            """);
        Assert.Empty(result.Errors);
        Assert.DoesNotContain("System.Xml", result.GeneratedText);
        Assert.DoesNotContain("System.Reflection", result.GeneratedText);
        Assert.DoesNotContain("RssParser", result.GeneratedText);
        Assert.DoesNotContain("RmlParser", result.GeneratedText);
        Assert.DoesNotContain("XmlReader", result.GeneratedText);
    }

    [Fact]
    public void ReefRuntimeDoesNotReferenceGenerator()
    {
        string[] references = typeof(UIBuilder<>).Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .ToArray();
        Assert.DoesNotContain("Reef.Generator", references);
    }
}
