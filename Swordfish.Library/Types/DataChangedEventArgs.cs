// ReSharper disable UnusedMember.Global
namespace Swordfish.Library.Types;

public struct DataChangedEventArgs<T>(in T oldValue, in T newValue)
{
    public static readonly DataChangedEventArgs<T> Empty = new();

    public readonly T OldValue = oldValue;

    public readonly T NewValue = newValue;
}