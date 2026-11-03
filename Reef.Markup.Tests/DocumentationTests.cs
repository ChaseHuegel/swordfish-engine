using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Xunit;

namespace Reef.Markup.Tests;

public class DocumentationTests
{
    [Fact]
    public void UserDocsExist()
    {
        string directory = UserDocsDirectory();
        Assert.True(File.Exists(Path.Combine(directory, "README.md")));
        Assert.True(File.Exists(Path.Combine(directory, "getting-started.md")));
        Assert.True(File.Exists(Path.Combine(directory, "examples.md")));
        Assert.True(File.Exists(Path.Combine(directory, "quick-reference.md")));
        Assert.True(File.Exists(Path.Combine(directory, "rider-setup.md")));
        Assert.True(File.Exists(Path.Combine(directory, "spec-traceability.md")));
    }

    [Fact]
    public void ExampleLinksResolve()
    {
        string directory = UserDocsDirectory();
        string text = File.ReadAllText(Path.Combine(directory, "examples.md"));
        foreach (Match match in Regex.Matches(text, "\\]\\(([^)]+)\\)"))
        {
            string target = match.Groups[1].Value;
            if (target.StartsWith("http", StringComparison.Ordinal) || target.StartsWith('#'))
            {
                continue;
            }

            string path = Path.GetFullPath(Path.Combine(directory, target));
            Assert.True(File.Exists(path), "Missing documentation link target: " + target);
        }
    }

    [Fact]
    public void QuickReferenceListsBuiltInTags()
    {
        string text = File.ReadAllText(Path.Combine(UserDocsDirectory(), "quick-reference.md"));
        foreach (string tag in new[] { "Element", "Text", "Image", "If", "ElseIf", "Else", "ForEach", "Empty", "Content" })
        {
            Assert.Contains("`" + tag + "`", text);
        }
    }

    private static string UserDocsDirectory([CallerFilePath] string path = "")
    {
        string repoRoot = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path)!, ".."));
        return Path.Combine(repoRoot, "userdocs", "reef");
    }
}
