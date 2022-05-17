using ImGuiNET;

namespace Swordfish.Engine.Rendering.UI
{
    public class Font
    {
        public int Size;

        public string MinUnicode;

        public string MaxUnicode;

        public bool IsIcons;

        public bool IsDefault;

        public string Name;

        public ImFontPtr Ptr;

        public string Source;

        public Font() {}
    }
}
