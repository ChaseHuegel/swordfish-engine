using Swordfish.Library.IO;
// ReSharper disable UnassignedField.Global

namespace Swordfish.UI
{
    public struct Font
    {
        // Tomlet requires an explicit constructor or it will throw on deserialization.
        // ReSharper disable once UnusedMember.Global
        public Font()
        {
        }

        public required int Size;

        public required string MinUnicode;

        public required string MaxUnicode;

        public required bool IsIcons;

        public required bool IsDefault;

        public required string Name;

        public required PathInfo Source;

        public required int TabSize;
    }
}
