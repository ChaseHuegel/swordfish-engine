using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging.Abstractions;
using Swordfish.Library.IO;
using Swordfish.Library.Util;
using WaywardBeyond.Data.Localization;
using Xunit;

namespace WaywardBeyond.Tests;

/// <summary>Tests for <see cref="LocalizedTags"/> and <see cref="LocalizedTagsDatabase"/> merging.</summary>
public class LocalizedTagsTests
{
    [Fact]
    public void ConstructorKeepsFirstOccurrenceOrderWithoutDuplicates()
    {
        var tags = new LocalizedTags(new Dictionary<string, List<string>>
        {
            ["alpha"] = ["b", "a", "b", "c", "a"],
            ["empty"] = [],
        });

        Assert.Equal(["b", "a", "c"], tags.GetValues("alpha")!.ToArray());
        Assert.Empty(tags.GetValues("empty")!);
    }

    [Fact]
    public void GetValuesReturnsNullForUnknownTag()
    {
        var tags = new LocalizedTags(new Dictionary<string, List<string>>());

        Assert.Null(tags.GetValues("missing"));
    }

    [Fact]
    public void ContainsResolvesKnownTagsOnly()
    {
        var tags = new LocalizedTags(new Dictionary<string, List<string>>
        {
            ["alpha"] = ["a"],
        });

        Assert.True(tags.Contains("alpha", "a"));
        Assert.False(tags.Contains("alpha", "b"));
        Assert.False(tags.Contains("missing", "a"));
    }

    [Fact]
    public void MergeAppendsOnlyNewValuesInOrder()
    {
        var destination = new LocalizedTags(new Dictionary<string, List<string>>
        {
            ["alpha"] = ["a", "b"],
        });
        var source = new LocalizedTags(new Dictionary<string, List<string>>
        {
            ["alpha"] = ["b", "c"],
            ["beta"] = ["y", "y"],
        });

        destination.Merge(source);

        Assert.Equal(["a", "b", "c"], destination.GetValues("alpha")!.ToArray());
        Assert.Equal(["y"], destination.GetValues("beta")!.ToArray());
        Assert.True(destination.Contains("alpha", "c"));
        Assert.False(destination.Contains("alpha", "d"));
    }

    [Fact]
    public void DatabaseAppendsInvariantTagsAfterLocalizedTags()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"wb-tags-tests-{Path.GetRandomFileName()}");
        string invariantDir = Path.Combine(tempDir, "lang", "tags");
        string localizedDir = Path.Combine(tempDir, "lang", "en", "tags");
        Directory.CreateDirectory(invariantDir);
        Directory.CreateDirectory(localizedDir);

        try
        {
            File.WriteAllText(
                Path.Combine(invariantDir, "invariant.toml"),
                """
                "Language" = ""

                [Tags]
                "alpha" = ["b", "a", "b"]
                "beta" = ["x"]
                """
            );
            File.WriteAllText(
                Path.Combine(localizedDir, "localized.toml"),
                """
                "Language" = "en"

                [Tags]
                "alpha" = ["local_b", "local_a", "b"]
                "beta" = ["local_x"]
                """
            );

            var vfs = new VirtualFileSystem();
            vfs.Mount(new PathInfo(tempDir));
            IFileParser[] parsers = [new LocalizedTagDefinitionParser()];
            var parseService = new VirtualFileParseService(parsers, vfs);
            var database = new LocalizedTagsDatabase(NullLogger<LocalizedTagsDatabase>.Instance, parseService, vfs);

            LocalizedTags invariant = database.Invariant!;
            Assert.Equal(["b", "a"], invariant.GetValues("alpha")!.ToArray());

            Result<LocalizedTags> localizedResult = database.Get("en");
            Assert.True(localizedResult.Success);
            LocalizedTags localized = localizedResult.Value;
            Assert.Equal(["local_b", "local_a", "b", "a"], localized.GetValues("alpha")!.ToArray());
            Assert.Equal(["local_x", "x"], localized.GetValues("beta")!.ToArray());
            Assert.True(localized.Contains("alpha", "a"));
            Assert.True(localized.Contains("beta", "local_x"));
            Assert.False(localized.Contains("alpha", "local_c"));
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }
}
