using System;
using System.Reflection;

namespace Swordfish.Library.Reflection;

internal record struct TypedBindingFlags(in Type Type, in BindingFlags BindingFlags)
{
    public readonly Type Type = Type;

    public readonly BindingFlags BindingFlags = BindingFlags;
}