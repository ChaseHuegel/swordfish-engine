using DryIoc;
using Shoal.DependencyInjection;

namespace Swordfish.Demo;

// ReSharper disable once UnusedType.Global
public class Module : IDryIocModule
{
    public void Load(IContainer container)
    {
        container.RegisterMany<Demo>(ifAlreadyRegistered: IfAlreadyRegistered.AppendNewImplementation);
    }
}