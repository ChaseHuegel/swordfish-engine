using DryIoc;
using Shoal.DependencyInjection;

namespace Swordfish.Demo;

// ReSharper disable once UnusedType.Global
public class Injector : IDryIocInjector
{
    public void Inject(IContainer container)
    {
        container.RegisterMany<Demo>();
    }
}