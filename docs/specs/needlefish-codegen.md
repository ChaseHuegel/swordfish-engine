# Needlefish Codegen

One subject: how the repository compiles `.nsd` schemas into C# types.

## Packages

- `Needlefish` 1.5.0 — runtime attributes and reflection helpers.
- `Needlefish.Generators` 1.2.0 — Roslyn incremental source generator.

The generator version is independent of the runtime version. The generator
ships its own compiler and emits the same source as `nsdc`.

The repository does not run `nsdc` as a build step. Do not install `nsdc` to
build the repository.

## Schema layout

All schemas live in one project:

```text
WaywardBeyond.Data/
  Nsd/
    saves.nsd
    levels.nsd
    voxels.nsd
    components.nsd
    network.nsd
```

One project is required. The generator resolves includes only from files in the
same compilation. It also emits every `.nsd` file as a root. A cross-project
include would emit the same types in two assemblies. That breaks type identity
for shared models.

## Includes

The compiler requires an include for every referenced schema type. Includes
resolve relative to the including file.

- `levels.nsd` includes `saves.nsd`.
- `components.nsd` includes `saves.nsd`.
- `network.nsd` includes `voxels.nsd`.

`components.nsd` uses `WaywardBeyond.Data.ItemData`. `network.nsd` uses
`WaywardBeyond.Data.Voxel`.

## Generator options

`WaywardBeyond.Data.csproj` references the generator:

```xml
<PackageReference Include="Needlefish.Generators" Version="1.2.0" PrivateAssets="all" />
```

It also sets `AllowUnsafeBlocks` because the generated serializer uses unsafe
code.

Set `NeedlefishPartial` per file. The networked component types have
hand-written partial declarations, so their schemas are partial:

```xml
<ItemGroup>
    <NsdFiles Include="Nsd/components.nsd" NeedlefishPartial="true" />
    <NsdFiles Include="Nsd/network.nsd" NeedlefishPartial="true" />
</ItemGroup>
```

The default glob discovers every other `.nsd` file in the project. The
persistence schemas remain non-partial.

## Namespaces

The schema namespace becomes the generated C# namespace.

| Schema | Namespace |
|---|---|
| `saves.nsd`, `levels.nsd`, `voxels.nsd` | `WaywardBeyond.Data` |
| `components.nsd` | `WaywardBeyond.Data.Components` |
| `network.nsd` | `WaywardBeyond.Data.Networking` |

The hand-written component partials live in
`WaywardBeyond.Data/Components`. The registry marker types
`NetworkComponentAttribute` and `NetworkDirection` live in
`WaywardBeyond.Data/Registry` with namespace `WaywardBeyond.Data.Registry`.

Types that still live in `WaywardBeyond.Networking` keep the
`WaywardBeyond.Networking.*` namespaces. This includes `NetworkRegistry`,
`IPayloadCodec`, `NetworkComponent`, `PendingInputComponent`, and the stage
buffers.

## Generated output

The generator emits source in memory during compilation. The repository does
not write generated files to disk and does not commit them.

To inspect the generated source for one build, set
`EmitCompilerGeneratedFiles=true` and `CompilerGeneratedFilesOutputPath` on a
local build. Remove the settings before you commit.

## Adding or changing a message

1. Edit the matching `.nsd` file under `WaywardBeyond.Data/Nsd`.
2. Add an `#include` when the message references a type from another schema.
3. Keep field ids stable. Add new fields with new ids.
4. Update the C# consumer and the matching spec in `docs/specs/`.
5. Run `dotnet build`.

Do not edit generated source. The source does not exist in the tree.

## Source of truth

- Generator wiring: `WaywardBeyond.Data/WaywardBeyond.Data.csproj`
- Schemas: `WaywardBeyond.Data/Nsd/`
- Generated component partials: `WaywardBeyond.Data/Components/`
- Registry markers: `WaywardBeyond.Data/Registry/`
