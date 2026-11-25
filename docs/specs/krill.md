# Krill — Standalone ECS Package

One subject: the `Krill` package. It covers the package identity, the project
layout, the public surface, the boundary rule, and the pack steps.

## Identity

| Item | Value |
|---|---|
| Package id | `Krill` |
| Namespace | `Krill` |
| Framework | `net8.0` |
| Dependencies | None |
| License | Apache-2.0 |
| Version source | `Krill/Krill.csproj` |

Krill was previously `Swordfish.ECS`. It is a standalone library used by
Swordfish and Wayward Beyond. It does not depend on the engine or Shoal.

## Project layout

| Project | Role | Packable |
|---|---|---|
| `Krill/` | Library: `World`, `DataStore`, `Entity`, `Uuid`, `Ref<T>`, interfaces | Yes |
| `Krill.Generator/` | Roslyn generator for the component API permutations | No |
| `Krill.Tests/` | xunit tests for the package | No |
| `Krill.Benchmarks/` | BenchmarkDotNet query and system-tick benchmarks | No |

The generator runs at build time only. Its output is compiled into `Krill.dll`,
so consumers need the library and nothing else. `EmitCompilerGeneratedFiles`
writes the output under `Krill/obj/generated` for inspection. Never hand-edit
it.

## Public surface

| Type | Contract |
|---|---|
| `World` | System registry over one `DataStore`. `Tick` runs systems in order. |
| `DataStore` | Entity allocation, chunked component storage, queries, dirty flags. |
| `Entity` | Handle plus `Uuid`. Component calls delegate to the store. |
| `Uuid` | Stable entity identity for save and load. |
| `Ref<T>` | Component reference. `Write` sets the dirty flag. |
| `IDataComponent` | Marker for component structs. |
| `IEntitySystem` | System contract. `Tick(float delta, DataStore store)`. |

The generator emits `Alloc`, `AddOrUpdate`, `Remove`, the query families, and
`TryGet` for arity 1 to 4. The limit is `MaxComponents` in
`Krill.Generator/EcsComponentApiGenerator.cs:21`. The component API source of
truth is that generator. Change it there, not in generated code.

Query families: `Query`, `QueryRef`, `QueryDirty`, `QueryRefDirty`,
`QueryRemoved`, `QueryParallel`, `QueryDirtyParallel`. Use the struct-action
overload in hot paths. It avoids a delegate allocation.

`QueryRemoved<T>` reads the value captured when a live component was removed.
Captures live in a sparse per-store table keyed by entity index, so a recycled
entity index that adds the same component before the removal is acknowledged
cannot overwrite it and idle queries return in O(1). The query repeats the
removal until `ClearRemoved<T>(entity)` acknowledges it and releases the
captured value. `ClearDirty<T>` only clears the write flag. `DisposeRemoved()`
disposes and clears every pending removal whose component type implements
`IDisposable`, without boxing, on removal and entity free (not `AddOrUpdate`).
The engine uses this to release native resources from removed components; see
[resource-lifetime](resource-lifetime.md).

## Boundary rule

`Krill/` references no other project in this repo. Do not add an engine,
Swordfish, Shoal, or Wayward Beyond reference to it. Game and engine code
reference Krill, never the reverse.

The engine keeps its own `Swordfish.ECS` namespace in `Swordfish/ECS/` for the
engine components and systems. That namespace is not part of Krill.

## Consumer docs

`Krill/README.md` is the package readme. It is packed into the NuGet package and
documents install and usage. Keep it current with the public surface.

## Pack and publish

```bash
dotnet pack Krill/Krill.csproj -c Release -o .packages
```

The package holds `lib/net8.0/Krill.dll` and `README.md`. The nuspec records the
Apache-2.0 license expression and the repository URL. `.github/workflows/nuget.yml`
packs and pushes the solution output on a push to `main`.

## Tests

```bash
dotnet test Krill.Tests
```

The suite covers dirty tracking, query iteration, the generated API, and UUID
allocation. It has no dependency on the engine, so it runs on any platform.
