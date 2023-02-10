using System.Reflection;
using Swordfish.Library.IO;

namespace Swordfish.Extensibility;

public abstract class Mod : IPlugin
{
    public IPathService LocalPathService = new PathService(System.IO.Path.GetDirectoryName(Assembly.GetCallingAssembly().Location));

    public abstract string Name { get; }
    public abstract string Description { get; }

    public abstract void Initialize();

    public virtual void Load() { }
    public virtual void Unload() { }

}
