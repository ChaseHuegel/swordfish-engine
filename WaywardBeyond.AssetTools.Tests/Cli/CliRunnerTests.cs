using System.Text.Json;
using WaywardBeyond.AssetTools.Cli;

namespace WaywardBeyond.AssetTools.Tests.Cli;

/// <summary>
///     End-to-end CLI tests covering all five commands, JSON schemaVersion output, exit codes, and ANSI color gating.
/// </summary>
public sealed class CliRunnerTests : IDisposable
{
    private readonly string _tempRoot;

    /// <summary>
    ///     Creates an isolated temporary directory for CLI tests.
    /// </summary>
    public CliRunnerTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), $"wb-cli-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempRoot);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }

    [Fact]
    public void NewPackValidateRoundTripFormatAndIdsCommandsWorkEndToEnd()
    {
        string packDir = Path.Combine(_tempRoot, "demo-pack");

        //  1. new pack --json
        (int newCode, string newOut, string newErr) = RunCli(["new", "pack", packDir, "--id", "demo.pack", "--namespace", "demo", "--json"]);
        Assert.Equal(CliExitCode.CLEAN, newCode);
        Assert.Equal(string.Empty, newErr);
        using (JsonDocument doc = JsonDocument.Parse(newOut))
        {
            Assert.Equal(CliRunner.JSON_SCHEMA_VERSION, doc.RootElement.GetProperty("schemaVersion").GetInt32());
            Assert.Equal("demo.pack", doc.RootElement.GetProperty("id").GetString());
        }

        //  Add an unformatted valid brick + albedo texture.
        File.WriteAllBytes(Path.Combine(packDir, "assets", "textures", "block", "hull.png"), [1, 2, 3]);
        string brickFile = Path.Combine(packDir, "assets", "bricks", "hull.toml");
        File.WriteAllText(brickFile, "[[Bricks]]\r\nID=\"demo:hull\"\r\nShape=\"block\"\r\n[Bricks.Textures]\r\nDefault=[\"hull\"]\r\n");

        //  2. format --json
        (int fmtCode, string fmtOut, string fmtErr) = RunCli(["format", packDir, "--json"]);
        Assert.Equal(CliExitCode.CLEAN, fmtCode);
        Assert.Equal(string.Empty, fmtErr);
        using (JsonDocument doc = JsonDocument.Parse(fmtOut))
        {
            Assert.Equal(CliRunner.JSON_SCHEMA_VERSION, doc.RootElement.GetProperty("schemaVersion").GetInt32());
            Assert.True(doc.RootElement.GetProperty("formattedCount").GetInt32() >= 1);
        }

        //  3. roundtrip --check --json
        (int rtCode, string rtOut, _) = RunCli(["roundtrip", packDir, "--check", "--json"]);
        Assert.Equal(CliExitCode.CLEAN, rtCode);
        using (JsonDocument doc = JsonDocument.Parse(rtOut))
        {
            Assert.Equal(CliRunner.JSON_SCHEMA_VERSION, doc.RootElement.GetProperty("schemaVersion").GetInt32());
            Assert.Equal(0, doc.RootElement.GetProperty("mismatchCount").GetInt32());
        }

        //  4. validate --json (clean -> exit 0)
        (int valOkCode, string valOkOut, _) = RunCli(["validate", packDir, "--json"]);
        Assert.Equal(CliExitCode.CLEAN, valOkCode);
        using (JsonDocument doc = JsonDocument.Parse(valOkOut))
        {
            Assert.Equal(CliRunner.JSON_SCHEMA_VERSION, doc.RootElement.GetProperty("schemaVersion").GetInt32());
            Assert.False(doc.RootElement.GetProperty("blocksExport").GetBoolean());
        }

        //  5. ids --json
        (int idsCode, string idsOut, _) = RunCli(["ids", packDir, "--json"]);
        Assert.Equal(CliExitCode.CLEAN, idsCode);
        using (JsonDocument doc = JsonDocument.Parse(idsOut))
        {
            Assert.Equal(CliRunner.JSON_SCHEMA_VERSION, doc.RootElement.GetProperty("schemaVersion").GetInt32());
            Assert.Equal("demo:hull", doc.RootElement.GetProperty("assets").GetProperty("Brick")[0].GetProperty("id").GetString());
        }

        //  6. Introduce broken finding -> validate returns exit 1 and disables ANSI color when useColor=false.
        File.WriteAllText(
            Path.Combine(packDir, "assets", "bricks", "broken.toml"),
            "[[Bricks]]\nID = \"demo:broken\"\nShape = \"NotValid\"\n");
        (int valFailCode, string valFailOut, _) = RunCli(["validate", packDir], useColor: false);
        Assert.Equal(CliExitCode.FINDINGS_OR_MISMATCH, valFailCode);
        Assert.DoesNotContain("\u001b[", valFailOut, StringComparison.Ordinal);

        //  7. Introduce syntax error -> roundtrip --check returns exit 1.
        File.WriteAllText(Path.Combine(packDir, "assets", "bricks", "corrupt.toml"), "[[Bricks]]\nID = \"unterminated");
        (int rtFailCode, _, _) = RunCli(["roundtrip", packDir, "--check"]);
        Assert.Equal(CliExitCode.FINDINGS_OR_MISMATCH, rtFailCode);
    }

    [Fact]
    public void InvalidUsageAndMissingDirectoriesReturnExitCodeTwoOnStderr()
    {
        (int emptyCode, string emptyOut, string emptyErr) = RunCli([]);
        Assert.Equal(CliExitCode.USAGE_OR_IO_ERROR, emptyCode);
        Assert.Equal(string.Empty, emptyOut);
        Assert.NotEmpty(emptyErr);

        (int missingCode, _, string missingErr) = RunCli(["validate", Path.Combine(_tempRoot, "does-not-exist")]);
        Assert.Equal(CliExitCode.USAGE_OR_IO_ERROR, missingCode);
        Assert.NotEmpty(missingErr);
    }

    private static (int ExitCode, string StdOut, string StdErr) RunCli(string[] args, bool useColor = false)
    {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var runner = new CliRunner(stdout, stderr, useColor);
        int code = runner.Run(args);
        return (code, stdout.ToString(), stderr.ToString());
    }
}
