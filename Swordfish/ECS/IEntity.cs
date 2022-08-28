namespace Swordfish.ECS;

public interface IEntity
{
    ulong ID { get; }

    string Name { get; set; }

    string Tag { get; set; }
}
