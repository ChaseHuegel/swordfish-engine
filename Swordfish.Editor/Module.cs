using DryIoc;
using Shoal.DependencyInjection;

namespace Swordfish.Editor;

// ReSharper disable once UnusedType.Global
public class Module : IDryIocModule
{
    public void Load(IContainer container)
    {
        container.RegisterMany<Editor>();
    }
}