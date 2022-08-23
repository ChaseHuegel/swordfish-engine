namespace Swordfish.Plugins;

public interface IPlugin
{

    void Load();

    void Unload();

    void Initialize();
}
