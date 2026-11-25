using System.Text;
using Swordfish.Library.Util;
using WaywardBeyond.AssetTools.Storage;

namespace WaywardBeyond.AssetTools.Tests.Storage;

/// <summary>
///     Tests draft recovery, external modification conflicts, atomic single-file writes, and multi-file reverse rollback.
/// </summary>
public sealed class DraftAndAtomicSaveTests : IDisposable
{
    private readonly string _tempRoot;

    /// <summary>
    ///     Creates an isolated temporary test directory.
    /// </summary>
    public DraftAndAtomicSaveTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), $"wb-assettools-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempRoot);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }

    [Fact]
    public void AtomicSingleFileSaveWritesVerifiesAndLeavesNoTempFiles()
    {
        string targetPath = Path.Combine(_tempRoot, "assets", "core.toml");
        byte[] payload = Encoding.UTF8.GetBytes("ID = \"wb:core\"\r\n");

        var saver = new AtomicFileSaver();
        AtomicSaveResult result = saver.Save(new FileSaveRequest(targetPath, payload, ExpectFileNotToExist: true));

        Assert.True(result.Success);
        Assert.Equal(ContentHash.ComputeSha256Hex(payload), result.WrittenSha256);
        Assert.Equal(payload, File.ReadAllBytes(targetPath));

        string[] remainingFiles = Directory.GetFiles(Path.GetDirectoryName(targetPath)!);
        Assert.Single(remainingFiles);
    }

    [Fact]
    public void ExternalModificationConflictBlocksSaveWithoutAutoMerge()
    {
        string targetPath = Path.Combine(_tempRoot, "brick.toml");
        byte[] initial = Encoding.UTF8.GetBytes("ID = \"wb:v1\"\n");
        byte[] externalEdit = Encoding.UTF8.GetBytes("ID = \"wb:external\"\n");
        byte[] toolEdit = Encoding.UTF8.GetBytes("ID = \"wb:tool\"\n");

        File.WriteAllBytes(targetPath, initial);
        string baseHash = ContentHash.ComputeSha256Hex(initial);

        File.WriteAllBytes(targetPath, externalEdit);
        string expectedExternalHash = ContentHash.ComputeSha256Hex(externalEdit);

        var saver = new AtomicFileSaver();
        AtomicSaveResult result = saver.Save(new FileSaveRequest(targetPath, toolEdit, ExpectedBaseSha256: baseHash));

        Assert.False(result.Success);
        Assert.True(result.Conflict.HasValue);
        Assert.Equal(FileSaveConflictReason.ModifiedExternally, result.Conflict.Value.Reason);
        Assert.Equal(baseHash, result.Conflict.Value.ExpectedBaseSha256);
        Assert.Equal(expectedExternalHash, result.Conflict.Value.ActualDiskSha256);
        Assert.Equal(externalEdit, File.ReadAllBytes(targetPath));
    }

    [Fact]
    public void ExternalDeletionAndCreationConflictsReturnStructuredReasons()
    {
        var saver = new AtomicFileSaver();

        string deletedPath = Path.Combine(_tempRoot, "deleted.toml");
        string fakeBaseHash = ContentHash.ComputeSha256Hex("ID = \"old\"\n");
        AtomicSaveResult deletedResult = saver.Save(new FileSaveRequest(deletedPath, Encoding.UTF8.GetBytes("ID = \"new\"\n"), ExpectedBaseSha256: fakeBaseHash));
        Assert.False(deletedResult.Success);
        Assert.Equal(FileSaveConflictReason.DeletedExternally, deletedResult.Conflict!.Value.Reason);
        Assert.False(File.Exists(deletedPath));

        string createdPath = Path.Combine(_tempRoot, "created.toml");
        byte[] existing = Encoding.UTF8.GetBytes("ID = \"already_there\"\n");
        File.WriteAllBytes(createdPath, existing);
        AtomicSaveResult createdResult = saver.Save(new FileSaveRequest(createdPath, Encoding.UTF8.GetBytes("ID = \"mine\"\n"), ExpectFileNotToExist: true));
        Assert.False(createdResult.Success);
        Assert.Equal(FileSaveConflictReason.CreatedExternally, createdResult.Conflict!.Value.Reason);
        Assert.Equal(existing, File.ReadAllBytes(createdPath));
    }

    [Fact]
    public void DraftStoreDetectsUncleanExitRecoversDraftAndFlagsExternalChanges()
    {
        string draftsDir = Path.Combine(_tempRoot, "drafts");
        string targetPath = Path.Combine(_tempRoot, "workspace", "core.toml");
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);

        byte[] baseBytes = Encoding.UTF8.GetBytes("ID = \"wb:core\"\n");
        byte[] draftBytes = Encoding.UTF8.GetBytes("ID = \"wb:core_unsaved\"\n");
        File.WriteAllBytes(targetPath, baseBytes);
        string baseHash = ContentHash.ComputeSha256Hex(baseBytes);

        var crashingSession = new DraftStore(draftsDir, sessionId: "session-1");
        Assert.True(crashingSession.StartSession().Success);
        Assert.False(crashingSession.HadUncleanPreviousExit);
        Assert.True(crashingSession.SaveDraft(targetPath, draftBytes, baseHash).Success);

        var restartedSession = new DraftStore(draftsDir, sessionId: "session-2");
        Assert.True(restartedSession.StartSession().Success);
        Assert.True(restartedSession.HadUncleanPreviousExit);

        Result<IReadOnlyList<DraftRecoveryEntry>> recoveredClean = restartedSession.GetRecoverableDrafts();
        Assert.True(recoveredClean.Success);
        Assert.Single(recoveredClean.Value);
        Assert.Equal(draftBytes, recoveredClean.Value[0].DraftBytes);
        Assert.False(recoveredClean.Value[0].HasExternalModification);

        File.WriteAllBytes(targetPath, Encoding.UTF8.GetBytes("ID = \"wb:external_change\"\n"));
        Result<IReadOnlyList<DraftRecoveryEntry>> recoveredModified = restartedSession.GetRecoverableDrafts();
        Assert.True(recoveredModified.Success);
        Assert.True(recoveredModified.Value[0].HasExternalModification);

        Assert.True(restartedSession.DiscardDraft(targetPath).Success);
        Assert.True(restartedSession.MarkCleanShutdown().Success);

        var cleanSession = new DraftStore(draftsDir, sessionId: "session-3");
        Assert.True(cleanSession.StartSession().Success);
        Assert.False(cleanSession.HadUncleanPreviousExit);
    }

    [Fact]
    public void MultiFileSaveRollsBackInReverseOrderWhenLaterFileFails()
    {
        string recoveryDir = Path.Combine(_tempRoot, "recovery");
        string fileOne = Path.Combine(_tempRoot, "files", "one.toml");
        string fileTwo = Path.Combine(_tempRoot, "files", "two.toml");
        string fileThree = Path.Combine(_tempRoot, "files", "three.toml");
        Directory.CreateDirectory(Path.GetDirectoryName(fileOne)!);

        byte[] originalOne = Encoding.UTF8.GetBytes("ID = \"one_original\"\n");
        byte[] originalThree = Encoding.UTF8.GetBytes("ID = \"three_original\"\n");
        File.WriteAllBytes(fileOne, originalOne);
        File.WriteAllBytes(fileThree, originalThree);

        var innerSaver = new AtomicFileSaver();
        int callCount = 0;
        var multiSaver = new MultiFileSaver(
            recoveryDir,
            innerSaver,
            request =>
            {
                callCount++;
                if (callCount == 3)
                {
                    return AtomicSaveResult.FromFailure("Simulated IO failure on third file.");
                }

                return innerSaver.Save(in request);
            });

        List<FileSaveRequest> batch =
        [
            new FileSaveRequest(fileOne, Encoding.UTF8.GetBytes("ID = \"one_new\"\n"), ContentHash.ComputeSha256Hex(originalOne)),
            new FileSaveRequest(fileTwo, Encoding.UTF8.GetBytes("ID = \"two_created\"\n"), ExpectFileNotToExist: true),
            new FileSaveRequest(fileThree, Encoding.UTF8.GetBytes("ID = \"three_new\"\n"), ContentHash.ComputeSha256Hex(originalThree))
        ];

        MultiFileSaveResult result = multiSaver.SaveAll(batch);

        Assert.False(result.Success);
        Assert.True(result.RolledBack);
        Assert.Equal([Path.GetFullPath(fileTwo), Path.GetFullPath(fileOne)], result.RolledBackPaths);
        Assert.Equal(originalOne, File.ReadAllBytes(fileOne));
        Assert.False(File.Exists(fileTwo));
        Assert.Equal(originalThree, File.ReadAllBytes(fileThree));
        Assert.False(multiSaver.HasPendingRecoveryManifest());
    }

    [Fact]
    public void MultiFileSavePreFlightConflictLeavesAllFilesUntouched()
    {
        string recoveryDir = Path.Combine(_tempRoot, "recovery");
        string fileOne = Path.Combine(_tempRoot, "batch", "a.toml");
        string fileTwo = Path.Combine(_tempRoot, "batch", "b.toml");
        Directory.CreateDirectory(Path.GetDirectoryName(fileOne)!);

        byte[] origOne = Encoding.UTF8.GetBytes("ID = \"a\"\n");
        byte[] origTwo = Encoding.UTF8.GetBytes("ID = \"b_external\"\n");
        File.WriteAllBytes(fileOne, origOne);
        File.WriteAllBytes(fileTwo, origTwo);

        var multiSaver = new MultiFileSaver(recoveryDir);
        List<FileSaveRequest> batch =
        [
            new FileSaveRequest(fileOne, Encoding.UTF8.GetBytes("ID = \"a_updated\"\n"), ContentHash.ComputeSha256Hex(origOne)),
            new FileSaveRequest(fileTwo, Encoding.UTF8.GetBytes("ID = \"b_updated\"\n"), ContentHash.ComputeSha256Hex("ID = \"b_stale\"\n"))
        ];

        MultiFileSaveResult result = multiSaver.SaveAll(batch);

        Assert.False(result.Success);
        Assert.Single(result.Conflicts);
        Assert.Equal(Path.GetFullPath(fileTwo), result.Conflicts[0].FilePath);
        Assert.Equal(origOne, File.ReadAllBytes(fileOne));
        Assert.Equal(origTwo, File.ReadAllBytes(fileTwo));
        Assert.False(multiSaver.HasPendingRecoveryManifest());
    }

    [Fact]
    public void MultiFileSaveSucceedsAndClearsRecoveryManifest()
    {
        string recoveryDir = Path.Combine(_tempRoot, "recovery");
        string fileOne = Path.Combine(_tempRoot, "ok", "a.toml");
        string fileTwo = Path.Combine(_tempRoot, "ok", "b.toml");
        byte[] bytesOne = Encoding.UTF8.GetBytes("ID = \"a\"\n");
        byte[] bytesTwo = Encoding.UTF8.GetBytes("ID = \"b\"\n");

        var multiSaver = new MultiFileSaver(recoveryDir);
        MultiFileSaveResult result = multiSaver.SaveAll(
        [
            new FileSaveRequest(fileOne, bytesOne, ExpectFileNotToExist: true),
            new FileSaveRequest(fileTwo, bytesTwo, ExpectFileNotToExist: true)
        ]);

        Assert.True(result.Success);
        Assert.Equal(2, result.WrittenPaths.Count);
        Assert.Equal(bytesOne, File.ReadAllBytes(fileOne));
        Assert.Equal(bytesTwo, File.ReadAllBytes(fileTwo));
        Assert.False(multiSaver.HasPendingRecoveryManifest());
    }

    [Fact]
    public void RecoverPendingTransactionRestoresBackupsAndDeletesCreatedFilesAfterCrash()
    {
        string recoveryDir = Path.Combine(_tempRoot, "crash-recovery");
        string backupDir = Path.Combine(recoveryDir, "tx-crashed");
        string existingFile = Path.Combine(_tempRoot, "crashed", "existing.toml");
        string createdFile = Path.Combine(_tempRoot, "crashed", "created.toml");
        Directory.CreateDirectory(backupDir);
        Directory.CreateDirectory(Path.GetDirectoryName(existingFile)!);

        byte[] originalExisting = Encoding.UTF8.GetBytes("ID = \"before_crash\"\n");
        string backupPath = Path.Combine(backupDir, "0000.bak");
        File.WriteAllBytes(backupPath, originalExisting);
        File.WriteAllBytes(existingFile, Encoding.UTF8.GetBytes("ID = \"half_written\"\n"));
        File.WriteAllBytes(createdFile, Encoding.UTF8.GetBytes("ID = \"created_before_crash\"\n"));

        var multiSaver = new MultiFileSaver(recoveryDir);
        string manifest =
            $"BackupDirectory = \"{backupDir.Replace("\\", "\\\\")}\"\n\n" +
            "[[Entries]]\n" +
            $"DestinationPath = \"{existingFile.Replace("\\", "\\\\")}\"\n" +
            "ExistedBefore = true\n" +
            $"BackupPath = \"{backupPath.Replace("\\", "\\\\")}\"\n\n" +
            "[[Entries]]\n" +
            $"DestinationPath = \"{createdFile.Replace("\\", "\\\\")}\"\n" +
            "ExistedBefore = false\n" +
            "BackupPath = \"\"\n";
        File.WriteAllText(multiSaver.ManifestFilePath, manifest);

        Assert.True(multiSaver.HasPendingRecoveryManifest());
        Result recoverResult = multiSaver.RecoverPendingTransaction();

        Assert.True(recoverResult.Success);
        Assert.False(multiSaver.HasPendingRecoveryManifest());
        Assert.Equal(originalExisting, File.ReadAllBytes(existingFile));
        Assert.False(File.Exists(createdFile));
    }
}
