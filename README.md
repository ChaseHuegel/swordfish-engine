[![](https://img.shields.io/nuget/v/Swordfish?label=Swordfish)](https://www.nuget.org/packages/Swordfish/)
[![](https://img.shields.io/nuget/v/Swordfish.Library?label=Library)](https://www.nuget.org/packages/Swordfish.Library/)
[![](https://img.shields.io/nuget/v/Swordfish.Integrations?label=Integrations)](https://www.nuget.org/packages/Swordfish.Integrations/)
[![](https://img.shields.io/nuget/v/Swordfish.ECS?label=ECS)](https://www.nuget.org/packages/Swordfish.ECS/)
[![](https://img.shields.io/nuget/v/Needlefish?label=Needlefish)](https://www.nuget.org/packages/Needlefish/)
[![](https://img.shields.io/nuget/v/Shoal?label=Shoal)](https://www.nuget.org/packages/Shoal/)
[![](https://img.shields.io/nuget/v/Swordfish.Compilation?label=Compilation)](https://www.nuget.org/packages/Swordfish.Compilation/)

<p align="center">
  <a href="">
    <img alt="Swordfish" src="Swordfish/Manifest/banner.png">
  </a>
</p>

# Using
Silk.NET (OpenGL and ImGui wrappers) https://github.com/dotnet/Silk.NET

ImageSharp (Image loading) https://github.com/SixLabors/ImageSharp

glTF2Loader (GLTF parsing) https://github.com/KhronosGroup/glTF-CSharp-Loader

DryIoc (DI) https://github.com/dadhi/DryIoc

Tomlet (TOML files) https://github.com/SamboyCoding/Tomlet

Needlefish (My binary serializer) https://github.com/ChaseHuegel/needlefish

JoltPhysicsSharp ([Jolt Physics](https://github.com/jrouwe/JoltPhysics) C# bindings) https://github.com/amerkoleci/JoltPhysicsSharp/tree/main

# Current State
The engine is undergoing a full rewrite to accomplish the follow:

- Update to .NET 8.0
- Create an editor tool.
- Switch to Silk.NET as the OpenGL wrapper.
- Implement a full UI API.
- Implement a full networking API.
- Introduce extensibility through proper mod and plugin support.
- Make use of dependency injection. Rewriting the renderer resulted in rewriting the engine due to tight dependencies on the old renderer.