namespace Shoal.DependencyInjection;

public interface IDryIocInjector
{
    public void Inject(IContainer container);
}