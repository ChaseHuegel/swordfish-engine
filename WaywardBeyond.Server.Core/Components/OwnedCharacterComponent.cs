using Swordfish.ECS;

namespace WaywardBeyond.Server.Core.Components;

/// <summary>
/// Server-only marker binding a player mirror to the character id it represents, so the server can
/// persist the character's authoritative location under the <c>&lt;level&gt;.character.&lt;id&gt;</c> bucket
/// key on autosave/disconnect/shutdown. Clients own the <c>characters</c> bucket; the server only ever
/// stores the location of whoever is playing on a connection.
/// </summary>
public struct OwnedCharacterComponent(in ulong characterId) : IDataComponent
{
    public ulong CharacterId = characterId;
}