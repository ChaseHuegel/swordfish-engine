using Swordfish.Library.IO;
using Xunit;
using Xunit.Abstractions;

namespace Swordfish.Tests;

public class VirtualFileSystemTests : TestBase
{
    public VirtualFileSystemTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public void Test_VirtualFileSystem()
    {
        var fileService = new FileService([]);
        var vfs = new VirtualFileSystem();

        vfs.Mount(new PathInfo(@"TestFiles/VirtualFileSystem\assets1"));
        Assert.True(vfs.TryGetFile(new PathInfo("a.txt"), out PathInfo file1));
        Assert.Equal("assets1", fileService.ReadString(file1));
        
        vfs.Mount(new PathInfo(@"TestFiles\\VirtualFileSystem/assets2"));
        Assert.True(vfs.TryGetFile(new PathInfo("a.txt"), out PathInfo file2));
        Assert.Equal("assets2", fileService.ReadString(file2));
        
        vfs.Mount(new PathInfo(@"TestFiles\\VirtualFileSystem\\assets3.zip"));
        Assert.True(vfs.TryGetFile(new PathInfo("a.txt"), out PathInfo file3));
        Assert.Equal("assets3", fileService.ReadString(file3));
    }
}