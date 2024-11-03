using System.IO;
using System.IO.Compression;

namespace Swordfish.Library.IO;

internal class ZipArchiveEntryStream(in ZipArchive zip, in Stream stream) : Stream
{
    public override bool CanRead => _stream.CanRead;
    
    public override bool CanSeek => _stream.CanSeek;
    
    public override bool CanWrite => _stream.CanWrite;
    
    public override long Length => _stream.Length;

    public override long Position
    {
        get => _stream.Position; 
        set => _stream.Position = value;
    }
    
    private readonly ZipArchive _zip = zip;
    private readonly Stream _stream = stream;
    
    public override void Flush()
    {
        _stream.Flush();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        return _stream.Read(buffer, offset, count);
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        return _stream.Seek(offset, origin);
    }

    public override void SetLength(long value)
    {
        _stream.SetLength(value);
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        _stream.Write(buffer, offset, count);
    }

    protected override void Dispose(bool disposing)
    {
        _stream.Dispose();
        _zip.Dispose();
        base.Dispose(disposing);
    }
}