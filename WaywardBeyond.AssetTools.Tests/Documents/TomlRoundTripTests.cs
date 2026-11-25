using System.Text;
using WaywardBeyond.AssetTools.Documents;

namespace WaywardBeyond.AssetTools.Tests.Documents;

/// <summary>
///     Verifies byte-identical TOML parsing and no-op emission across every shipped file and encoding edge case.
/// </summary>
public sealed class TomlRoundTripTests
{
    [Fact]
    public void EveryShippedTomlFileRoundTripsByteIdentically()
    {
        string repoRoot = FindRepositoryRoot();
        List<string> tomlFiles = EnumerateShippedTomlFiles(repoRoot);

        Assert.True(tomlFiles.Count > 100, $"Expected > 100 shipped TOML files in '{repoRoot}', found {tomlFiles.Count}.");

        foreach (string filePath in tomlFiles)
        {
            byte[] originalBytes = File.ReadAllBytes(filePath);
            TomlDocument document = TomlDocument.Parse(originalBytes, filePath);

            Assert.False(
                document.HasSyntaxErrors,
                $"Shipped TOML file '{filePath}' reported syntax errors: {FormatDiagnostics(document.Diagnostics)}");

            byte[] emittedBytes = document.ToBytes();
            Assert.Equal(originalBytes, emittedBytes);

            for (int i = 0; i < document.AllFields.Count; i++)
            {
                TomlFieldSpan field = document.AllFields[i];
                Assert.Equal(field.RawKey, field.KeySpan.Slice(document.Text));
                Assert.Equal("=", field.EqualsSpan.Slice(document.Text));
                Assert.Equal(field.RawValue, field.ValueSpan.Slice(document.Text));
            }
        }
    }

    [Fact]
    public void RoundTripPreservesBomCrlfCommentsQuotesAndMissingTrailingNewline()
    {
        const string content =
            "# Header comment\r\n" +
            "\r\n" +
            "[[Bricks]]\r\n" +
            "ID    = 'wb:literal' # inline comment\r\n" +
            "Shape = \"block\"";
        byte[] preamble = Encoding.UTF8.GetPreamble();
        byte[] body = Encoding.UTF8.GetBytes(content);
        byte[] original = new byte[preamble.Length + body.Length];
        Buffer.BlockCopy(preamble, 0, original, 0, preamble.Length);
        Buffer.BlockCopy(body, 0, original, preamble.Length, body.Length);

        TomlDocument document = TomlDocument.Parse(original);

        Assert.False(document.HasSyntaxErrors);
        Assert.True(document.HasUtf8Bom);
        Assert.Equal("\r\n", document.LineEnding);
        Assert.False(document.HasTrailingNewline);
        Assert.Equal(original, document.ToBytes());
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current != null)
        {
            if (File.Exists(Path.Combine(current.FullName, "Swordfish.sln")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root containing Swordfish.sln.");
    }

    private static List<string> EnumerateShippedTomlFiles(string repoRoot)
    {
        List<string> result = [];
        Stack<string> pending = new();
        pending.Push(repoRoot);

        while (pending.Count > 0)
        {
            string directory = pending.Pop();
            foreach (string subDir in Directory.EnumerateDirectories(directory))
            {
                string name = Path.GetFileName(subDir);
                if (string.Equals(name, "bin", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(name, "obj", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(name, ".git", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(name, "BenchmarkDotNet.Artifacts", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                pending.Push(subDir);
            }

            foreach (string file in Directory.EnumerateFiles(directory, "*.toml"))
            {
                result.Add(file);
            }
        }

        result.Sort(StringComparer.OrdinalIgnoreCase);
        return result;
    }

    private static string FormatDiagnostics(IReadOnlyList<TomlDiagnostic> diagnostics)
    {
        return string.Join("; ", diagnostics.Select(static d => $"{d.Span.Line}:{d.Span.Column} {d.Message}"));
    }
}
