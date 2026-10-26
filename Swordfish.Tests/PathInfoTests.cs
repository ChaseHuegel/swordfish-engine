using System;
using System.IO;
using System.Text;
using Swordfish.Library.IO;
using Xunit;

namespace Swordfish.Tests;

public class PathInfoTests
{
    [Fact]
    public void WriteTruncatesExistingContent()
    {
        string path = Path.Combine(
            Path.GetTempPath(),
            $"swordfish-pathinfo-{Guid.NewGuid():N}.txt"
        );

        try
        {
            File.WriteAllText(path, "this content is longer than the replacement");
            var pathInfo = new PathInfo(path);
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes("short"));

            pathInfo.Write(stream);

            Assert.Equal("short", File.ReadAllText(path));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
