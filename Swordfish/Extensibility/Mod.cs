using System.Reflection;
using Swordfish.Library.IO;

namespace Swordfish.Extensibility;

public abstract class Mod : IPlugin
{
    public IPathService LocalPathService
    {
        get
        {
            // new RootedPathService(System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location));
            Assembly assembly = Assembly.GetCallingAssembly();
            Assembly assembly2 = Assembly.GetExecutingAssembly();
            Assembly assembly3 = Assembly.GetEntryAssembly();
            string location = assembly.Location;
            string directory = System.IO.Path.GetDirectoryName(location);
            return new RootedPathService(directory);
        }
    }

    public abstract string Name { get; }
    public abstract string Description { get; }

    public abstract void Start();

    public virtual void Unload() { }
}
