using Swordfish.Library.Util;
using WaywardBeyond.AssetTools.Documents;

namespace WaywardBeyond.AssetTools.Tests.Documents;

/// <summary>
///     Golden text tests for TOML CST mutations, alignment rules, and explicit formatting.
/// </summary>
public sealed class TomlMutationGoldenTests
{
    [Fact]
    public void AddFieldRealignsOnlyEditedAlignedTableAndLeavesOtherTablesUntouched()
    {
        const string input =
            "# Paint palette\n" +
            "\n" +
            "[[Paints]]\n" +
            "ID    = \"wb:rust\"\n" +
            "Name  = \"paint.rust.name\"\n" +
            "Color = \"#be4a2f\"\n" +
            "\n" +
            "[[Paints]]\n" +
            "ID    = \"wb:cream\"\n" +
            "Name  = \"paint.cream.name\"\n" +
            "Color = \"#ead4aa\"\n" +
            "\n" +
            "[Meta]\n" +
            "Author = \"Test\"\n" +
            "Version = 1\n";

        TomlDocument document = TomlDocument.Parse(input);
        Result<TomlTableSpan> firstPaint = document.GetTable("Paints", occurrenceIndex: 0);
        Assert.True(firstPaint.Success);
        Assert.True(firstPaint.Value.IsColumnAligned);

        Result addResult = document.AddField(firstPaint.Value, "Connected", "true");
        Assert.True(addResult.Success);

        const string expected =
            "# Paint palette\n" +
            "\n" +
            "[[Paints]]\n" +
            "ID        = \"wb:rust\"\n" +
            "Name      = \"paint.rust.name\"\n" +
            "Color     = \"#be4a2f\"\n" +
            "Connected = true\n" +
            "\n" +
            "[[Paints]]\n" +
            "ID    = \"wb:cream\"\n" +
            "Name  = \"paint.cream.name\"\n" +
            "Color = \"#ead4aa\"\n" +
            "\n" +
            "[Meta]\n" +
            "Author = \"Test\"\n" +
            "Version = 1\n";

        Assert.Equal(expected, document.Text);
    }

    [Fact]
    public void AddFieldToUnalignedTableDoesNotReformatAlignment()
    {
        const string input =
            "[Meta]\n" +
            "ID = \"pack\"\n" +
            "Description = \"Long description\"\n";

        TomlDocument document = TomlDocument.Parse(input);
        TomlTableSpan meta = document.GetTable("Meta").Value;
        Assert.False(meta.IsColumnAligned);

        Result addResult = document.AddField(meta, "Author", "\"Tester\"");
        Assert.True(addResult.Success);

        const string expected =
            "[Meta]\n" +
            "ID = \"pack\"\n" +
            "Description = \"Long description\"\n" +
            "Author = \"Tester\"\n";

        Assert.Equal(expected, document.Text);
    }

    [Fact]
    public void RemoveFieldShrinksAlignedTableAndPreservesPrecedingComments()
    {
        const string input =
            "[[Bricks]]\n" +
            "# Keep this comment\n" +
            "ID          = \"wb:light\"\n" +
            "Shape       = \"any\"\n" +
            "CastShadows = false\n";

        TomlDocument document = TomlDocument.Parse(input);
        TomlFieldSpan castShadows = document.GetField("Bricks", "CastShadows").Value;

        Result removeResult = document.RemoveField(castShadows);
        Assert.True(removeResult.Success);

        const string expected =
            "[[Bricks]]\n" +
            "# Keep this comment\n" +
            "ID    = \"wb:light\"\n" +
            "Shape = \"any\"\n";

        Assert.Equal(expected, document.Text);
    }

    [Fact]
    public void EnumChangePreservesInlineCommentsAndQuoteStyle()
    {
        const string input =
            "[[Bricks]]\n" +
            "ID    = 'wb:core'\n" +
            "Shape = \"block\" # default shape\n";

        TomlDocument document = TomlDocument.Parse(input);
        TomlFieldSpan shapeField = document.GetField("Bricks", "Shape").Value;
        TomlFieldSpan idField = document.GetField("Bricks", "ID").Value;

        Assert.True(document.SetString(shapeField, "any").Success);
        idField = document.GetField("Bricks", "ID").Value;
        Assert.True(document.SetString(idField, "wb:core_v2").Success);

        const string expected =
            "[[Bricks]]\n" +
            "ID    = 'wb:core_v2'\n" +
            "Shape = \"any\" # default shape\n";

        Assert.Equal(expected, document.Text);
    }

    [Fact]
    public void AppendArrayItemHandlesInlineAndMultilineLayouts()
    {
        const string input =
            "[Bricks.Sounds]\n" +
            "Place = [\"metal_place\"]\n" +
            "Break = []\n" +
            "[Bricks.Textures]\n" +
            "Variants = [\n" +
            "    \"panel.0\",\n" +
            "    \"panel.1\",\n" +
            "]\n";

        TomlDocument document = TomlDocument.Parse(input);

        Assert.True(document.AppendArrayStringItem(document.GetField("Bricks.Sounds", "Place").Value, "metal_place_2").Success);
        Assert.True(document.AppendArrayStringItem(document.GetField("Bricks.Sounds", "Break").Value, "metal_break").Success);
        Assert.True(document.AppendArrayStringItem(document.GetField("Bricks.Textures", "Variants").Value, "panel.2").Success);

        const string expected =
            "[Bricks.Sounds]\n" +
            "Place = [\"metal_place\", \"metal_place_2\"]\n" +
            "Break = [\"metal_break\"]\n" +
            "[Bricks.Textures]\n" +
            "Variants = [\n" +
            "    \"panel.0\",\n" +
            "    \"panel.1\",\n" +
            "    \"panel.2\",\n" +
            "]\n";

        Assert.Equal(expected, document.Text);
    }

    [Fact]
    public void ReorderArrayItemsPreservesFormattingAndQuotes()
    {
        const string input =
            "[Tags]\n" +
            "\"brick.light\" = [\n" +
            "    \"wb:first\",\n" +
            "    'wb:second',\n" +
            "    \"wb:third\",\n" +
            "]\n";

        TomlDocument document = TomlDocument.Parse(input);
        TomlFieldSpan tagField = document.GetField("Tags", "brick.light").Value;

        Result moveResult = document.MoveArrayItem(tagField, fromIndex: 2, toIndex: 0);
        Assert.True(moveResult.Success);

        const string expected =
            "[Tags]\n" +
            "\"brick.light\" = [\n" +
            "    \"wb:third\",\n" +
            "    \"wb:first\",\n" +
            "    'wb:second',\n" +
            "]\n";

        Assert.Equal(expected, document.Text);
    }

    [Fact]
    public void AppendArrayTableMatchesExistingAlignment()
    {
        const string input =
            "[[Paints]]\n" +
            "ID    = \"wb:rust\"\n" +
            "Color = \"#be4a2f\"\n";

        TomlDocument document = TomlDocument.Parse(input);
        Result<TomlTableSpan> appended = document.AppendArrayTable(
            "Paints",
            [
                new KeyValuePair<string, string>("ID", "\"wb:gold\""),
                new KeyValuePair<string, string>("Name", "\"paint.gold.name\""),
                new KeyValuePair<string, string>("Color", "\"#ffcc00\"")
            ]);

        Assert.True(appended.Success);
        Assert.Equal(1, appended.Value.OccurrenceIndex);

        const string expected =
            "[[Paints]]\n" +
            "ID    = \"wb:rust\"\n" +
            "Color = \"#be4a2f\"\n" +
            "\n" +
            "[[Paints]]\n" +
            "ID    = \"wb:gold\"\n" +
            "Name  = \"paint.gold.name\"\n" +
            "Color = \"#ffcc00\"\n";

        Assert.Equal(expected, document.Text);
    }

    [Fact]
    public void RenameKeyAndRenameStringValueUpdateAcrossSpans()
    {
        const string input =
            "[[Bricks]]\n" +
            "ID    = \"wb:old_brick\"\n" +
            "Shape = \"block\"\n" +
            "\n" +
            "[Tags]\n" +
            "\"brick.buildable\" = [\"wb:old_brick\", \"wb:other\"]\n";

        TomlDocument document = TomlDocument.Parse(input);
        Result<int> renamedValues = document.RenameStringValue("wb:old_brick", "wb:new_brick");
        Assert.True(renamedValues.Success);
        Assert.Equal(2, renamedValues.Value);

        TomlFieldSpan shapeField = document.GetField("Bricks", "Shape").Value;
        Assert.True(document.RenameKey(shapeField, "BrickShape").Success);

        const string expected =
            "[[Bricks]]\n" +
            "ID         = \"wb:new_brick\"\n" +
            "BrickShape = \"block\"\n" +
            "\n" +
            "[Tags]\n" +
            "\"brick.buildable\" = [\"wb:new_brick\", \"wb:other\"]\n";

        Assert.Equal(expected, document.Text);
    }

    [Fact]
    public void ExplicitFormatterNormalizesLineEndingsSpacingAndAlignment()
    {
        const string input =
            "# Comment stays\r\n" +
            "[[Bricks]]   \r\n" +
            "ID=\"wb:core\"\r\n" +
            "CastShadows   =   false\r\n" +
            "[Bricks.Textures]\r\n" +
            "Default=[\r\n" +
            "  \"core\"\r\n" +
            "]\r\n";

        Result<string> formatted = TomlFormatter.Format(input);
        Assert.True(formatted.Success);

        const string expected =
            "# Comment stays\n" +
            "[[Bricks]]\n" +
            "ID          = \"wb:core\"\n" +
            "CastShadows = false\n" +
            "\n" +
            "[Bricks.Textures]\n" +
            "Default = [\n" +
            "    \"core\",\n" +
            "]\n";

        Assert.Equal(expected, formatted.Value);
    }
}
