using Swordfish.Library.Annotations;

namespace Swordfish.Library.IO
{
    public struct InputDevice
    {
        public int Index;

        [NotNull]
        public string Name;

        public InputDevice(int index, string name)
        {
            Index = index;
            Name = name;
        }
    }
}
