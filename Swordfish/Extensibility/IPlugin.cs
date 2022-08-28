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
    ///     Invoked when first loaded prior to any initialization.
    ///     This is where you should load resources and bind dependencies.
    /// </summary>
    void Load();

    /// <summary>
    ///     Invoked when unloaded.
    ///     This is where you should cleanup resources and unbind dependencies.
    /// </summary>
    void Unload();

    /// <summary>
    ///     Invoked after all adjacent extensions have loaded.
    ///     This is where you should get dependencies and bring the extension to life.
    /// </summary>
    void Initialize();
}