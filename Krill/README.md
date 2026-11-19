# Krill

A zero-dependency, engine-agnostic, struct-based Entity-Component-System (ECS)
for .NET.

Krill stores components in chunked, contiguous arrays. It offers generated
overloads for up to four components. The hot loops iterate only the live slot
range of each chunk.

## Install

```bash
dotnet add package Krill
```

Krill targets `net8.0` and has no dependencies.

## Quick start

Define components as structs that implement `IDataComponent`.

```csharp
using Krill;

public struct Position : IDataComponent
{
    public float X;
    public float Y;
}

public struct Velocity : IDataComponent
{
    public float X;
    public float Y;
}
```

Write a system. The system reads and writes components through a `DataStore`.

```csharp
public sealed class MoveSystem : IEntitySystem
{
    public void Tick(float delta, DataStore store)
    {
        store.QueryRef<Position, Velocity>(delta,
            static (float dt, DataStore store, int entity,
                    ref Ref<Position> position, in Velocity velocity) =>
            {
                position.Write.X += velocity.X * dt;
                position.Write.Y += velocity.Y * dt;
            });
    }
}
```

Create a world, add the system, and tick the world.

```csharp
var world = new World();
world.AddSystem(new MoveSystem());

Entity entity = world.NewEntity();
entity.AddOrUpdate(new Position(), new Velocity { X = 10f, Y = 0f });

world.Tick(0.016f);
```

## Main types

| Type | Role |
|---|---|
| `World` | Holds a `DataStore` and a set of systems. `Tick` runs the systems in order. |
| `DataStore` | Allocates entities and stores components in chunks. |
| `Entity` | A lightweight handle. It holds an entity id and its store. |
| `Uuid` | A stable identifier for an entity. It survives save and load. |
| `IDataComponent` | Marks a struct as a component. |
| `IEntitySystem` | A system. `Tick` receives the delta time and the store. |
| `Ref<T>` | A component reference. `Write` marks the component dirty. |

## Queries

`DataStore` generates these query families for arity 1 to 4:

| Method | Purpose |
|---|---|
| `Query` | Read every matching entity. |
| `QueryRef` | Read and write every matching entity. |
| `QueryDirty` | Read entities with dirty components. |
| `QueryRefDirty` | Read and write entities with dirty components. |
| `QueryRemoved` | Read the last known data of removed components. |
| `QueryParallel` | Read with `Parallel.For` for large sets. |
| `QueryDirtyParallel` | Dirty variant of `QueryParallel`. |

Each family has a delegate overload and a struct-action overload. Use the
struct-action overload in hot paths. It avoids a delegate allocation and lets
the JIT inline the call.

The source generator that builds these APIs runs at build time. The generated
members are compiled into `Krill.dll`. Consumers do not need the generator.

## Entity operations

`Entity` and `DataStore` expose generated overloads for 1 to 4 components:

```csharp
entity.Add<Position>();
entity.AddOrUpdate(new Position { X = 1f }, new Velocity { Y = 2f });
entity.Remove<Velocity>();
bool has = entity.Has<Position>();
bool found = entity.TryGet(out Position position);
```

`DataStore` also offers non-generic operations by `Type`, a `Find` predicate
search, and dirty-state access through `IsDirty`, `MarkDirty`, and
`ClearDirty`.

## Benchmarks

The benchmarks live in `Krill.Benchmarks` in the Swordfish Engine repository.
They cover query iteration and system ticks with BenchmarkDotNet.

## License

Apache-2.0. See [LICENSE](https://github.com/ChaseHuegel/swordfish-engine/blob/main/LICENSE).
