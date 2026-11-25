using System.Text;

namespace WaywardBeyond.AssetTools.Validation;

/// <summary>
///     Aggregated workspace validation findings with severity counts and plain-text formatting.
/// </summary>
public sealed class ValidationReport
{
    /// <summary>
    ///     Creates an immutable validation report from ordered <paramref name="findings"/>.
    /// </summary>
    public ValidationReport(IReadOnlyList<ValidationFinding> findings)
    {
        List<ValidationFinding> sorted = [.. findings];
        sorted.Sort(static (left, right) =>
        {
            int sevCompare = left.Severity.CompareTo(right.Severity);
            if (sevCompare != 0)
            {
                return sevCompare;
            }

            int rootCompare = StringComparer.OrdinalIgnoreCase.Compare(left.RootId, right.RootId);
            if (rootCompare != 0)
            {
                return rootCompare;
            }

            int fileCompare = StringComparer.OrdinalIgnoreCase.Compare(left.FilePath, right.FilePath);
            if (fileCompare != 0)
            {
                return fileCompare;
            }

            int lineCompare = left.Line.CompareTo(right.Line);
            if (lineCompare != 0)
            {
                return lineCompare;
            }

            return StringComparer.Ordinal.Compare(left.Code, right.Code);
        });

        Findings = sorted;

        int fatal = 0;
        int broken = 0;
        int crossPlatform = 0;
        int style = 0;
        int info = 0;
        for (int i = 0; i < sorted.Count; i++)
        {
            switch (sorted[i].Severity)
            {
                case ValidationSeverity.Fatal:
                    fatal++;
                    break;

                case ValidationSeverity.Broken:
                    broken++;
                    break;

                case ValidationSeverity.CrossPlatform:
                    crossPlatform++;
                    break;

                case ValidationSeverity.Style:
                    style++;
                    break;

                case ValidationSeverity.Informational:
                    info++;
                    break;
            }
        }

        FatalCount = fatal;
        BrokenCount = broken;
        CrossPlatformCount = crossPlatform;
        StyleCount = style;
        InformationalCount = info;
    }

    /// <summary>
    ///     Gets all findings sorted by severity, root, file, and line.
    /// </summary>
    public IReadOnlyList<ValidationFinding> Findings { get; }

    /// <summary>
    ///     Gets the count of fatal findings.
    /// </summary>
    public int FatalCount { get; }

    /// <summary>
    ///     Gets the count of broken findings.
    /// </summary>
    public int BrokenCount { get; }

    /// <summary>
    ///     Gets the count of cross-platform findings.
    /// </summary>
    public int CrossPlatformCount { get; }

    /// <summary>
    ///     Gets the count of style findings.
    /// </summary>
    public int StyleCount { get; }

    /// <summary>
    ///     Gets the count of informational findings.
    /// </summary>
    public int InformationalCount { get; }

    /// <summary>
    ///     Gets a value indicating whether fatal or broken findings block pack export.
    /// </summary>
    public bool BlocksExport => FatalCount > 0 || BrokenCount > 0;

    /// <summary>
    ///     Gets a value indicating whether non-blocking cross-platform or style warnings exist.
    /// </summary>
    public bool HasWarnings => CrossPlatformCount > 0 || StyleCount > 0;

    /// <summary>
    ///     Formats all findings into a plain-text report grouped by severity, root, and file.
    /// </summary>
    public string FormatPlainText()
    {
        var builder = new StringBuilder();
        builder.Append($"Summary: {FatalCount} fatal, {BrokenCount} broken, {CrossPlatformCount} cross-platform, {StyleCount} style, {InformationalCount} informational\n");

        ValidationSeverity? currentSeverity = null;
        string? currentRoot = null;
        string? currentFile = null;

        for (int i = 0; i < Findings.Count; i++)
        {
            ValidationFinding finding = Findings[i];
            if (currentSeverity != finding.Severity)
            {
                currentSeverity = finding.Severity;
                currentRoot = null;
                currentFile = null;
                builder.Append($"\n[{finding.Severity}]\n");
            }

            if (!string.Equals(currentRoot, finding.RootId, StringComparison.Ordinal))
            {
                currentRoot = finding.RootId;
                currentFile = null;
                builder.Append($"  Root: {finding.RootId}\n");
            }

            if (!string.Equals(currentFile, finding.FilePath, StringComparison.Ordinal))
            {
                currentFile = finding.FilePath;
                builder.Append($"    File: {finding.FilePath}\n");
            }

            string fieldSuffix = string.IsNullOrEmpty(finding.Field) ? string.Empty : $" ({finding.Field})";
            builder.Append($"      L{finding.Line}:{finding.Column} {finding.Code}{fieldSuffix}: {finding.Message}\n");
        }

        return builder.ToString();
    }
}
