using Swordfish.Library.Configuration;
using Swordfish.Library.Types;

namespace WaywardBeyond.Config;

/// <summary>Chat behavior configuration.</summary>
public sealed class ChatConfig : Config<ChatConfig>
{
    /// <summary>The number of milliseconds until new chats become stale.</summary>
    public DataBinding<int> StaleMs { get; private set; } = new(10_000);
    
    /// <summary>The max number of chat line history to retain.</summary>
    public DataBinding<int> MaxHistory { get; private set; } = new(100);
}