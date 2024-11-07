namespace Swordfish.Compilation.Lexing;

public readonly struct Token<T>(in T type, in string value)
    where T : struct
{
    public readonly T Type = type;
    public readonly string Value = value;
}