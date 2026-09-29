using System;
using WaywardBeyond.Shared.Data;
using Xunit;

namespace Swordfish.Tests;

public class SaveMigratorTests
{
    private sealed class V3ToV4Dummy : SaveMigration<DummyRecord>
    {
        public override uint FromVersion => 3;
        public override uint ToVersion => 4;
        public override DummyRecord ApplyValue(DummyRecord value) => value with { Migrated = true };
    }

    private sealed record DummyRecord(bool Migrated);

    [Fact]
    public void CurrentVersionRecordPassesThroughWithoutMigration()
    {
        SaveMigrator migrator = new([new V3ToV4Dummy()]);
        var record = new DummyRecord(false);

        DummyRecord result = migrator.Migrate(record, SaveVersion.CurrentDataVersion);

        Assert.False(result.Migrated);
    }

    [Fact]
    public void OlderVersionRecordRunsStepUpToCurrent()
    {
        SaveMigrator migrator = new([new V3ToV4Dummy()]);
        var record = new DummyRecord(false);

        DummyRecord result = migrator.Migrate(record, 3);

        Assert.True(result.Migrated);
    }

    [Fact]
    public void NewerVersionRecordIsRefused()
    {
        SaveMigrator migrator = new([new V3ToV4Dummy()]);

        Assert.Throws<SaveDataNotSupportedException>(() => migrator.Migrate(new DummyRecord(false), SaveVersion.CurrentDataVersion + 5));
    }

    [Fact]
    public void NonContiguousChainIsRejected()
    {
        var bad = new SkipToV5Dummy();
        Assert.Throws<InvalidOperationException>(() => new SaveMigrator([bad]));
    }

    private sealed class SkipToV5Dummy : SaveMigration<DummyRecord>
    {
        public override uint FromVersion => 3;
        public override uint ToVersion => 5;
        public override DummyRecord ApplyValue(DummyRecord value) => value;
    }

    [Fact]
    public void TypeWithoutMigrationPassesThroughAnySupportedVersion()
    {
        //  Character has no registered migration in v4, so an older same-shaped record is a no-op.
        SaveMigrator migrator = new([]);
        var character = new Character(
            new WaywardBeyond.Shared.Data.Version(SaveVersion.CurrentDataVersion, "t", "Development"),
            1, 0, 0, "n", 1, 1, 1, 1, 1, 1, 1, 0, GameMode.Creative, _Statistics: null, _Inventory: null
        );

        Character result = migrator.Migrate(character, 3);

        Assert.Equal(character.Version.DataVersion, result.Version.DataVersion);
        Assert.Equal(character.Id, result.Id);
    }

    [Fact]
    public void IsSupportedRejectsNewerVersions()
    {
        SaveMigrator migrator = new([new V3ToV4Dummy()]);

        Assert.True(migrator.IsSupported(3));
        Assert.True(migrator.IsSupported(SaveVersion.CurrentDataVersion));
        Assert.False(migrator.IsSupported(SaveVersion.CurrentDataVersion + 1));
    }
}