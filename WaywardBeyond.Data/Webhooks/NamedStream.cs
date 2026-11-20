using System;
using System.IO;
using System.Threading.Tasks;

namespace WaywardBeyond.Data.Webhooks;

/// <summary>
///     Associates a file name with an open stream for archive or webhook uploads.
/// </summary>
public readonly record struct NamedStream(string Name, Stream Value) : IDisposable, IAsyncDisposable
{
    /// <inheritdoc/>
    public void Dispose()
    {
        Value.Dispose();
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await Value.DisposeAsync();
    }
}
