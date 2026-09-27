using Swordfish.Library.Configuration;
using Swordfish.Library.Types;

namespace WaywardBeyond.Shared.Config;

/// <summary>
/// Chat behavior settings: how long the closed chat overlay lingers after activity, how many messages
/// the client retains for scrollback, and how many recent lines the closed overlay shows.
/// </summary>
public sealed class ChatSettings : Config<ChatSettings>
{
    public DataBinding<int> TimeoutSeconds { get; private set; } = new(10);
    public DataBinding<int> MaxHistory { get; private set; } = new(100);
    public DataBinding<int> MaxVisibleLines { get; private set; } = new(4);
}