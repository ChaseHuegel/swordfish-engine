using DryIoc;
using Shoal.DependencyInjection;

namespace Swordfish.Editor;

// ReSharper disable once UnusedType.Global
public class Injector : IDryIocInjector
{
    public void Inject(IContainer container)
    {
        container.RegisterMany<Editor>();
    }
}