using System;
using System.IO;
using Swordfish.Library.Util;

namespace WaywardBeyond.Shared.Networking.Transport;

public sealed class FrameStream(in Stream stream) : IDisposable
{
    private readonly Stream _stream = stream;

    public void Dispose()
    {
        _stream.Dispose();
    }

    public Result WriteFrame(byte[] data)
    {
        try
        {
            byte[] buffer = new byte[4 + data.Length];
            byte[] lengthDelimiter = BitConverter.GetBytes(buffer.Length);
            lengthDelimiter.CopyTo(buffer, 0);
            
            data.CopyTo(buffer, lengthDelimiter.Length);
            
            _stream.Write(buffer);
            return Result.FromSuccess();
        }
        catch (Exception ex)
        {
            return new Result(success: false, "Failed to write frame.", ex);
        }
    }

    public Result<byte[]> ReadFrame()
    {
        try
        {
            byte[] lengthBytes = new byte[4];
            for (int i = 0; i < 4; i++)
            {
                if (!TryReadNextByte(out byte value))
                {
                    return Result<byte[]>.FromFailure("Reached end of stream.");
                }

                lengthBytes[i] = value;
            }
            
            int length = BitConverter.ToInt32(lengthBytes, 0) - 4;
            byte[] buffer = new byte[length];
            for (int i = 0; i < buffer.Length; i++)
            {
                if (!TryReadNextByte(out byte value))
                {
                    return Result<byte[]>.FromFailure("Reached end of stream.");
                }
                
                buffer[i] = value;
            }
            
            return Result<byte[]>.FromSuccess(buffer);
        }
        catch (Exception ex)
        {
            return new Result<byte[]>(success: false, value: null!, "Failed to read frame.", ex);
        }
    }

    private bool TryReadNextByte(out byte value)
    {
        int read = _stream.ReadByte();
        if (read == -1)
        {
            value = 0;
            return false;
        }
        
        value = (byte)read;
        return true;
    }
}
