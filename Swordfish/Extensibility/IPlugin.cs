using Swordfish.Library.Collections;

namespace Swordfish.Extensibility;

public interface IPlugin
{
    /// <summary>
    ///     The name of this extension.
    /// </summary>
    string Name { get; }

    /// <summary>
    ///     A short description of what this extension is and does.
    /// </summary>
    string Description { get; }

    /// <summary>
    ///     Invoked after all adjacent extensions have loaded and dependencies resolved.
    ///     This is where you should bring the extension to life.
    /// </summary>
    void Start();

    /// <summary>
    ///     Invoked when unloaded.
    ///     This is where you should cleanup resources.
    /// </summary>
    void Unload();
}
