using WaywardBeyond.AssetTools.Workspace;
using GameContentVersion = WaywardBeyond.Data.ContentVersion;

namespace WaywardBeyond.AssetTools.Packs;

/// <summary>
///     Evaluated compatibility status and read-only decision for an opened workspace.
/// </summary>
public readonly record struct PackVersionAssessment(
    PackVersionStatus Status,
    uint? PackContentVersion,
    uint ToolContentVersion,
    bool IsReadOnly,
    string Message)
{
    /// <summary>
    ///     Assesses compatibility between a workspace mode, optional <c>pack.toml</c>, and tool content version.
    /// </summary>
    public static PackVersionAssessment Assess(
        WorkspaceMode mode,
        PackMetadata? metadata,
        uint toolContentVersion = GameContentVersion.CURRENT)
    {
        if (mode == WorkspaceMode.Repo)
        {
            return new PackVersionAssessment(
                PackVersionStatus.NotApplicableRepo,
                null,
                toolContentVersion,
                IsReadOnly: false,
                "Repository workspace does not require pack.toml versioning.");
        }

        if (!metadata.HasValue)
        {
            return new PackVersionAssessment(
                PackVersionStatus.UnversionedWarning,
                null,
                toolContentVersion,
                IsReadOnly: false,
                "Pack has no pack.toml metadata. Editing is enabled; export will stamp metadata in the staged copy.");
        }

        uint packVersion = metadata.Value.ContentVersion;
        if (packVersion > toolContentVersion)
        {
            return new PackVersionAssessment(
                PackVersionStatus.NewerReadOnly,
                packVersion,
                toolContentVersion,
                IsReadOnly: true,
                $"Pack targets ContentVersion {packVersion}, which is newer than this tool ({toolContentVersion}). Opened read-only.");
        }

        if (packVersion < toolContentVersion)
        {
            return new PackVersionAssessment(
                PackVersionStatus.OlderWarning,
                packVersion,
                toolContentVersion,
                IsReadOnly: false,
                $"Pack targets older ContentVersion {packVersion} (tool is {toolContentVersion}). Full editing enabled.");
        }

        return new PackVersionAssessment(
            PackVersionStatus.Compatible,
            packVersion,
            toolContentVersion,
            IsReadOnly: false,
            $"Pack matches tool ContentVersion {toolContentVersion}.");
    }
}
