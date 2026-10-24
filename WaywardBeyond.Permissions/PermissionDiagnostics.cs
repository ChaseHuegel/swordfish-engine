using System.Collections.Generic;

namespace WaywardBeyond.Permissions;

/// <summary>Collects non-fatal warnings and errors raised while loading and compiling permission files.</summary>
public sealed class PermissionDiagnostics
{
    private readonly List<string> _warnings = [];
    private readonly List<string> _errors = [];

    /// <summary>The non-fatal issues found while loading and compiling.</summary>
    public IReadOnlyList<string> Warnings => _warnings;

    /// <summary>The errors found while loading and compiling.</summary>
    public IReadOnlyList<string> Errors => _errors;

    /// <summary>Records a warning. Startup continues.</summary>
    internal void Warn(string message)
    {
        _warnings.Add(message);
    }

    /// <summary>Records an error. The policy loads with the affected entry corrupted or truncated.</summary>
    internal void Error(string message)
    {
        _errors.Add(message);
    }
}
