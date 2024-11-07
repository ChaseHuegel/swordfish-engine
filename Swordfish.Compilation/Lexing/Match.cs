namespace Swordfish.Compilation.Lexing;

public readonly struct Match<T>(in bool success, in int length, in Token<T> token)
    where T : struct
{
    public readonly bool Success = success;
    public readonly int Length = length;
    public readonly Token<T> Token = token;
}