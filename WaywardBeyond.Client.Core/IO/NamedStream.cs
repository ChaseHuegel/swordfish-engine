using System;
using System.IO;
using System.Threading.Tasks;

namespace WaywardBeyond.Client.Core.IO;

internal readonly record struct NamedStream(string Name, Stream Value) : IDisposable, IAsyncDisposable
{
    public void Dispose()
    {
        Value.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        await Value.DisposeAsync();
    }
}