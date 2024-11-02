using ImGuiNET;
using Swordfish.Library.IO;

namespace Swordfish.UI
{
    public struct Font
    {
        public Font()
        {
        }

        public int Size;

        public string MinUnicode;

        public string MaxUnicode;

        public bool IsIcons;

        public bool IsDefault;

        public string Name;

        public IPath Source;
    }
}
