namespace Swordfish.Compilation.Linting;

public readonly struct Issue(in string message, in IssueLevel level = IssueLevel.Error)
{
    public readonly string Message = message;
    public readonly IssueLevel Level = level;
}