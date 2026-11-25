using Swordfish.Library.Util;
using WaywardBeyond.AssetTools.Documents;

namespace WaywardBeyond.AssetTools.Tests.Documents;

/// <summary>
///     Verifies invalid-token survival during edits and safe raw-text handling of corrupt TOML files.
/// </summary>
public sealed class TomlInvalidAndCorruptionTests
{
    [Fact]
    public void InvalidFieldTokensSurviveUnrelatedEditsByteIdentically()
    {
        const string input =
            "[[Bricks]]\n" +
            "ID       = \"wb:broken_brick\"\n" +
            "Shape    = \"NotARealEnumValue\" # bad enum kept intact\n" +
            "MaxStack = \"not_a_number\"\n" +
            "Color    = 'definitely-not-hex'\n";

        TomlDocument document = TomlDocument.Parse(input);
        Assert.False(document.HasSyntaxErrors);

        TomlFieldSpan idField = document.GetField("Bricks", "ID").Value;
        Assert.True(document.SetString(idField, "wb:still_broken").Success);

        TomlTableSpan table = document.GetTable("Bricks").Value;
        Assert.True(document.AddField(table, "Passable", "true").Success);

        const string expected =
            "[[Bricks]]\n" +
            "ID       = \"wb:still_broken\"\n" +
            "Shape    = \"NotARealEnumValue\" # bad enum kept intact\n" +
            "MaxStack = \"not_a_number\"\n" +
            "Color    = 'definitely-not-hex'\n" +
            "Passable = true\n";

        Assert.Equal(expected, document.Text);
    }

    [Fact]
    public void SyntaxBreakingRawEditIsRejectedWithoutMutatingDocument()
    {
        const string input = "ID = \"wb:core\"\n";
        TomlDocument document = TomlDocument.Parse(input);

        Result result = document.SetRawValue(document.GetRootField("ID").Value, "\"unterminated");

        Assert.False(result.Success);
        Assert.Equal(input, document.Text);
        Assert.False(document.HasSyntaxErrors);
    }

    [Theory]
    [InlineData("[[Bricks]]\nID = \"wb:unterminated")]
    [InlineData("[[Bricks]]\nTextures = [\"a\", \"b\"")]
    [InlineData("[[Bricks\nID = \"wb:core\"\n")]
    [InlineData("[[Bricks]]\nID =\n")]
    public void TruncatedFilesOpenAsRawTextAndWriteBackUnchanged(string corruptContent)
    {
        TomlDocument document = TomlDocument.Parse(corruptContent);

        Assert.True(document.HasSyntaxErrors);
        Assert.NotEmpty(document.Diagnostics);
        Assert.Equal(corruptContent, document.Text);
        Assert.Equal(System.Text.Encoding.UTF8.GetBytes(corruptContent), document.ToBytes());

        Result addResult = document.AddRootField("Other", "\"value\"");
        Assert.False(addResult.Success);
        Assert.Equal(corruptContent, document.Text);

        Result<string> formatResult = TomlFormatter.Format(document);
        Assert.False(formatResult.Success);
    }

    [Fact]
    public void DuplicateKeysFlagSyntaxErrorsAndPreserveRawText()
    {
        const string duplicateKeyContent =
            "[[Bricks]]\n" +
            "ID = \"wb:first\"\n" +
            "ID = \"wb:second\"\n";

        TomlDocument document = TomlDocument.Parse(duplicateKeyContent);

        Assert.True(document.HasSyntaxErrors);
        Assert.NotEmpty(document.Diagnostics);
        Assert.Equal(duplicateKeyContent, document.Text);
    }

    [Theory]
    [InlineData("[Bricks]\nID = \"wb:one\"\n\n[[Bricks]]\nID = \"wb:two\"\n")]
    [InlineData("Bricks = \"scalar\"\n\n[Bricks]\nID = \"wb:one\"\n")]
    public void WrongTableTypesFlagSyntaxErrorsAndPreserveRawText(string wrongTableContent)
    {
        TomlDocument document = TomlDocument.Parse(wrongTableContent);

        Assert.True(document.HasSyntaxErrors);
        Assert.NotEmpty(document.Diagnostics);
        Assert.Equal(wrongTableContent, document.Text);
    }
}
