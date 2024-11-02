namespace Swordfish.Compilation.Lexing;

public readonly struct Token<T> where T : struct
{
    public readonly T Type;
    public readonly string Value;

    public Token(T type, string value)
    {
        Type = type;
        Value = value;
    }
}