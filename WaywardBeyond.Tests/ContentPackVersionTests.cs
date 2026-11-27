using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.Logging;
using Shoal.Modularity;
using Swordfish.Library.IO;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Packs;
using Xunit;

namespace WaywardBeyond.Tests;

/// <summary>Verifies that loaded content packs warn on <see cref="ContentVersion"/> skew and stay silent otherwise.</summary>
public class ContentPackVersionTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"waywardbeyond-pack-{Path.GetRandomFileName()}");

    /// <summary>Verifies that a pack built for the current content version does not warn.</summary>
    [Fact]
    public void Check_EqualVersion_DoesNotWarn()
    {
        ContentPackVersionChecker checker = CreateChecker(ContentVersion.CURRENT, out RecordingLogger<ContentPackVersionChecker> logger);

        checker.Run();

        Assert.Empty(logger.Entries);
    }

    /// <summary>Verifies that a pack built for an older content version warns without failing.</summary>
    [Fact]
    public void Check_OlderVersion_LogsWarning()
    {
        uint packVersion = ContentVersion.CURRENT - 1;
        ContentPackVersionChecker checker = CreateChecker(packVersion, out RecordingLogger<ContentPackVersionChecker> logger);

        checker.Run();

        (LogLevel level, string message) = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, level);
        Assert.Contains($"ContentVersion {packVersion}", message);
        Assert.Contains($"expects {ContentVersion.CURRENT}", message);
    }

    /// <summary>Verifies that a pack built for a newer content version warns without failing.</summary>
    [Fact]
    public void Check_NewerVersion_LogsWarning()
    {
        uint packVersion = ContentVersion.CURRENT + 1;
        ContentPackVersionChecker checker = CreateChecker(packVersion, out RecordingLogger<ContentPackVersionChecker> logger);

        checker.Run();

        (LogLevel level, string message) = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, level);
        Assert.Contains($"ContentVersion {packVersion}", message);
        Assert.Contains($"expects {ContentVersion.CURRENT}", message);
    }

    /// <summary>Verifies that a module without <c>pack.toml</c> keeps the pre-metadata behavior and stays silent.</summary>
    [Fact]
    public void Check_MissingMetadata_DoesNotWarn()
    {
        Directory.CreateDirectory(_root);
        var logger = new RecordingLogger<ContentPackVersionChecker>();
        var catalog = new ModuleCatalog();
        catalog.Add(new LoadedModule("waywardbeyond.test", "Test Pack", new PathInfo(_root)));
        var checker = new ContentPackVersionChecker(catalog, logger);

        checker.Run();

        Assert.Empty(logger.Entries);
    }

    private ContentPackVersionChecker CreateChecker(uint packVersion, out RecordingLogger<ContentPackVersionChecker> logger)
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(
            Path.Combine(_root, ContentPackMetadata.FILE_NAME),
            $"ID = \"waywardbeyond.test\"\nNamespace = \"test\"\nContentVersion = {packVersion}\n");

        logger = new RecordingLogger<ContentPackVersionChecker>();
        var catalog = new ModuleCatalog();
        catalog.Add(new LoadedModule("waywardbeyond.test", "Test Pack", new PathInfo(_root)));
        return new ContentPackVersionChecker(catalog, logger);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }
        catch
        {
            //  Best-effort cleanup.
        }
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Entries.Add((logLevel, formatter(state, exception)));
        }
    }
}
