namespace Swordfish.Compilation.Lexing;

public readonly struct Match<T> where T : struct
{
    public readonly bool Success;
    public readonly int Length;
    public readonly Token<T> Token;

    public Match(bool success, int length, Token<T> token)
    {
        Success = success;
        Length = length;
        Token = token;
    }
}