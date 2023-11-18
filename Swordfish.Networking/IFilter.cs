namespace Swordfish.Networking;

public interface IFilter<T>
{
    bool Check(T target);
}
