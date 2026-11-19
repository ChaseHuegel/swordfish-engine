using Krill;

namespace Swordfish.Audio;

public struct AudioSource(string id) : IDataComponent
{
    public string ID = id;
}