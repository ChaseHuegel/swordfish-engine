namespace Shoal.Modularity;

internal interface IModulesLoader
{
    void Load(Action<Assembly> hookCallback);
}