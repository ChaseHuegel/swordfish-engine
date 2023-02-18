using System.Reflection;
using Swordfish.Library.IO;

namespace Swordfish.Extensibility;

public abstract class Plugin : IPlugin
{
    public IPathService LocalPathService { get; } = new RootedPathService(System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location));

    public abstract string Name { get; }
    public abstract string Description { get; }

    public abstract void Start();

    public virtual void Unload() { }
}
