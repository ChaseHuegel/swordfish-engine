namespace Krill;

public interface IEntitySystem
{
    void Tick(float delta, DataStore store);
}
