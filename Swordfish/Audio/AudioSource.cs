namespace Swordfish.Audio;

public class AudioSource(in Stream stream) : IDisposable
{
    private readonly Stream _stream = stream;
    private readonly object _lock = new();

    public void Dispose()
    {
        _stream.Dispose();
    }
    
    public Stream CreateStream()
    {
        lock (_lock)
        {
            var memoryStream = new MemoryStream(capacity: (int)_stream.Length);
            _stream.CopyTo(memoryStream);
            _stream.Position = 0;
            return memoryStream;
        }
    }
}