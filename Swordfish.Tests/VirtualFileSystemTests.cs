using System.IO;
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
    public void Mount_Directory_FileExists()
    {
        var vfs = new VirtualFileSystem();

        vfs.Mount(new PathInfo(@"TestFiles/VirtualFileSystem\assets1"));

        Assert.True(vfs.FileExists(new PathInfo("a.txt")));
        Assert.True(vfs.FileExists(new PathInfo("subfolder1/b.txt")));
    }
    
    [Fact]
    public void Mount_Zip_FileExists()
    {
        var vfs = new VirtualFileSystem();

        vfs.Mount(new PathInfo(@"TestFiles\\VirtualFileSystem\\assets3.zip"));

        Assert.True(vfs.FileExists(new PathInfo("a.txt")));
        Assert.True(vfs.FileExists(new PathInfo("subfolder1/b.txt")));
    }
    
    [Fact]
    public void Mount_Archive_FileExists()
    {
        var vfs = new VirtualFileSystem();

        vfs.Mount(new PathInfo(@"TestFiles\\VirtualFileSystem\\assets4.pak"));

        Assert.True(vfs.FileExists(new PathInfo("a.txt")));
        Assert.True(vfs.FileExists(new PathInfo("subfolder1/b.txt")));
    }

    [Fact]
    public void Mount_DirectoryAndArchive_NewestOverrides()
    {
        var vfs = new VirtualFileSystem();

        vfs.Mount(new PathInfo(@"TestFiles/VirtualFileSystem\assets1"));
        Assert.True(vfs.TryGetFile(new PathInfo("a.txt"), out PathInfo file1));
        Assert.Equal("assets1", file1.ReadString());
        
        vfs.Mount(new PathInfo(@"TestFiles\\VirtualFileSystem/assets2"));
        Assert.True(vfs.TryGetFile(new PathInfo("a.txt"), out PathInfo file2));
        Assert.Equal("assets2", file2.ReadString());
        
        vfs.Mount(new PathInfo(@"TestFiles\\VirtualFileSystem\\assets3.zip"));
        Assert.True(vfs.TryGetFile(new PathInfo("a.txt"), out PathInfo file3));
        Assert.Equal("assets3", file3.ReadString());
        
        vfs.Mount(new PathInfo(@"TestFiles/VirtualFileSystem/assets4.pak"));
        Assert.True(vfs.TryGetFile(new PathInfo("a.txt"), out PathInfo file4));
        Assert.Equal("assets4", file4.ReadString());
    }

    [Fact]
    public void GetFiles_TopDirectory()
    {
        var vfs = new VirtualFileSystem();
        
        vfs.Mount(new PathInfo(@"TestFiles/VirtualFileSystem/assets1"));
        PathInfo[] files = vfs.GetFiles("", SearchOption.TopDirectoryOnly);
        
        Assert.Single(files);
        Assert.Equal("TestFiles/VirtualFileSystem/assets1/a.txt", files[0]);
    }
    
    [Fact]
    public void GetFiles_AllDirectories()
    {
        var vfs = new VirtualFileSystem();
        
        vfs.Mount(new PathInfo(@"TestFiles/VirtualFileSystem/assets1"));
        PathInfo[] files = vfs.GetFiles("", SearchOption.AllDirectories);
        
        Assert.Equal(2, files.Length);
        Assert.Equal("TestFiles/VirtualFileSystem/assets1/a.txt", files[0]);
        Assert.Equal("TestFiles/VirtualFileSystem/assets1/subfolder1/b.txt", files[1]);
    }
}