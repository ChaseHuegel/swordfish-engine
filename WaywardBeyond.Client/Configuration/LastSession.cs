using Swordfish.Library.Types;

namespace WaywardBeyond.Client.Configuration;

/// <summary>The last played session: where it ran, who played it, and which save it used.</summary>
public sealed class LastSession
{
    /// <summary>The server the last session ran on.</summary>
    public DataBinding<SessionServer> Server { get; private set; } = new(new SessionServer());

    /// <summary>The UUID of the colonist the last session played.</summary>
    public DataBinding<string> CharacterUuid { get; private set; } = new(string.Empty);

    /// <summary>The save the last session used.</summary>
    public DataBinding<SessionSave> Save { get; private set; } = new(new SessionSave());
}
