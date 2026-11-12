#nullable enable
using Shoal.CommandLine;
using Xunit;

namespace Swordfish.Tests;

public class CommandLineParserTests
{
    [Fact]
    public void QuotedArgvEntryKeepsSpaces()
    {
        CommandLineArgs args = new CommandLineParser().Parse(["--name", "Smoke Test"]);

        Assert.True(args.TryGetValue("name", out string? name));
        Assert.Equal("Smoke Test", name);
        Assert.Equal(["Smoke Test"], args.GetValues("name"));
    }

    [Fact]
    public void DoubleQuotedValueKeepsSpaces()
    {
        CommandLineArgs args = new CommandLineParser().Parse("--name \"Smoke Test\"");

        Assert.True(args.TryGetValue("name", out string? name));
        Assert.Equal("Smoke Test", name);
        Assert.Equal(["Smoke Test"], args.GetValues("name"));
    }

    [Fact]
    public void SingleQuotedValueKeepsSpaces()
    {
        CommandLineArgs args = new CommandLineParser().Parse("--name 'Smoke Test'");

        Assert.True(args.TryGetValue("name", out string? name));
        Assert.Equal("Smoke Test", name);
        Assert.Equal(["Smoke Test"], args.GetValues("name"));
    }

    [Fact]
    public void EqualsFormKeepsQuotedSpaces()
    {
        CommandLineArgs args = new CommandLineParser().Parse("--name=\"Smoke Test\"");

        Assert.True(args.TryGetValue("name", out string? name));
        Assert.Equal("Smoke Test", name);
    }

    [Fact]
    public void UnquotedMultiWordValueIsNotTruncated()
    {
        CommandLineArgs args = new CommandLineParser().Parse(["--name", "Smoke", "Test"]);

        Assert.True(args.TryGetValue("name", out string? name));
        Assert.Equal("Smoke Test", name);
    }

    [Fact]
    public void SeparateValuesStaySeparate()
    {
        CommandLineArgs args = new CommandLineParser().Parse(["--tags", "alpha", "beta"]);

        Assert.True(args.TryGetValues("tags", out string[]? tags));
        Assert.Equal(["alpha", "beta"], tags);
    }

    [Fact]
    public void QuotedValueCoexistsWithFlagsAndEqualsForm()
    {
        CommandLineArgs args = new CommandLineParser().Parse(["--verbose", "--port=9000", "--name", "Smoke Test"]);

        Assert.True(args.GetFlag("verbose"));
        Assert.True(args.TryGetValue("port", out int port));
        Assert.Equal(9000, port);
        Assert.True(args.TryGetValue("name", out string? name));
        Assert.Equal("Smoke Test", name);
    }
}
