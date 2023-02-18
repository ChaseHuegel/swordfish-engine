namespace Swordfish.UI.Elements;

public interface IFlagsProperty<TEnum> where TEnum : struct, Enum
{
    TEnum Flags { get; set; }
}
