using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Reef.Generator.Tests;

public class DiagnosticDocumentationTests
{
    [Fact]
    public void QuickReferenceListsEveryDescriptor()
    {
        string path = Path.Combine(RepositoryDirectory(), "userdocs", "reef", "quick-reference.md");
        string text = File.ReadAllText(path);

        foreach (FieldInfo field in typeof(DiagnosticDescriptors).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            if (field.GetValue(null) is DiagnosticDescriptor descriptor)
            {
                Assert.Contains(descriptor.Id, text);
            }
        }
    }

    private static string RepositoryDirectory([CallerFilePath] string path = "")
    {
        return Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path)!, ".."));
    }
}
