// ReSharper disable UnusedType.Global
// ReSharper disable UnusedMember.Global
namespace Swordfish.Compilation.Linting;

public interface ILinter<in T> where T : struct
{
    public Issue[] Lint(T[] items);
}