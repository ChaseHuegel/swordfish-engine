namespace Swordfish.ECS;

public interface IEntitySystem
{
    void Tick(float delta, DataStore store);
}
