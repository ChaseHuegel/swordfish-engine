using DryIoc;
using Shoal.DependencyInjection;
using WaywardBeyond.Client.Core.Bricks;

namespace WaywardBeyond.Client.Core;

// ReSharper disable once UnusedType.Global
public class Injector : IDryIocInjector
{
    public void Inject(IContainer container)
    {
        container.RegisterMany<Entry>();
    }
}