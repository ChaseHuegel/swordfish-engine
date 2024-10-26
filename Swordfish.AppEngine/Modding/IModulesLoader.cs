namespace Swordfish.AppEngine.Modding;

internal interface IModulesLoader
{
    void Load(Action<Assembly> hookCallback);
}