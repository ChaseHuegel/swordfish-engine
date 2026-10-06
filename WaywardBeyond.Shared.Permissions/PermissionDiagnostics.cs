using System.Collections.Generic;

namespace WaywardBeyond.Shared.Permissions;

/// <summary>Collects non-fatal warnings and errors raised while loading and compiling permission files.</summary>
public sealed class PermissionDiagnostics
{
    private readonly List<string> _warnings = [];
    private readonly List<string> _errors = [];

    public IReadOnlyList<string> Warnings => _warnings;

    public IReadOnlyList<string> Errors => _errors;

    public bool HasIssues => _warnings.Count > 0 || _errors.Count > 0;

    internal void Warn(string message)
    {
        _warnings.Add(message);
    }

    internal void Error(string message)
    {
        _errors.Add(message);
    }
}
