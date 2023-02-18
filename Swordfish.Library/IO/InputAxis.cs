using Swordfish.Library.Annotations;

namespace Swordfish.Library.IO
{
    public struct InputAxis
    {
        public int Index;

        [NotNull]
        public string Name;

        public InputAxis(int index, string name)
        {
            Index = index;
            Name = name;
        }
    }
}
