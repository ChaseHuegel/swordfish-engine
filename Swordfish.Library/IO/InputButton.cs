using Swordfish.Library.Annotations;

namespace Swordfish.Library.IO
{
    public struct InputButton
    {
        public int Index;

        [NotNull]
        public string Name;

        public InputButton(int index, string name)
        {
            Index = index;
            Name = name;
        }
    }
}
