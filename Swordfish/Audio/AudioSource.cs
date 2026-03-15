using Swordfish.ECS;

namespace Swordfish.Audio;

public struct AudioSource(string id) : IDataComponent
{
    public string ID = id;
}