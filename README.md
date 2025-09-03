[![](https://img.shields.io/nuget/v/Swordfish?label=Swordfish)](https://www.nuget.org/packages/Swordfish/)
[![](https://img.shields.io/nuget/v/Swordfish.Library?label=Library)](https://www.nuget.org/packages/Swordfish.Library/)
[![](https://img.shields.io/nuget/v/Swordfish.Integrations?label=Integrations)](https://www.nuget.org/packages/Swordfish.Integrations/)
[![](https://img.shields.io/nuget/v/Swordfish.ECS?label=ECS)](https://www.nuget.org/packages/Swordfish.ECS/)
[![](https://img.shields.io/nuget/v/Needlefish?label=Needlefish)](https://www.nuget.org/packages/Needlefish/)
[![](https://img.shields.io/nuget/v/Shoal?label=Shoal)](https://www.nuget.org/packages/Shoal/)
[![](https://img.shields.io/nuget/v/Swordfish.Compilation?label=Compilation)](https://www.nuget.org/packages/Swordfish.Compilation/)
[![](https://img.shields.io/nuget/v/Reef?label=Reef)](https://www.nuget.org/packages/Reef/)

<p align="center">
  <a href="">
    <img alt="Swordfish" src="Swordfish/Manifest/banner.png">
  </a>
</p>

# Projects

## Reef
Reef is a 0-dependency, renderer-agnostic IMGUI library that supports dynamic and flexible layout rules comparable to typical retained-mode GUIs. This is an alpha API under development as a replacement for Swordfish's currently implemented Dear ImGui-based UI implementation.

## Shoal
Shoal is a modular, and mod-friendly, application host that powers Swordfish. This provides Dependency Injection, containerization, C# scripting, and a standardized module system which implicitly provides modding support to applications. Applications are built from one or more modules themselves, and so are effectively "mods" of Shoal themselves.

## Swordfish
This is the Shoal module for the engine itself. This implements all engine-specific and visual functionality such as rendering, ECS systems, input, physics, and UI.

## Swordfish.Compilation
This is an API for compilation tooling that provides types for lexing, linting, and parsing. This will eventually provide implementations for C# scripting used by Shoal and Swordfish.

## Swordfish.Demo
This is a module for Swordfish which is used for experimenting and showcasing some engine capabilities. It is considered to be a sandbox more than a tech demo at this time. This changes often, can be messy, and regularly includes in-preview engine capabilities.

## Swordfish.ECS
This is a 0-dependency, engine-agnostic API for a struct-based Entity-Component-System implementation which is used by Swordfish.

## Swordfish.Editor
This is a module for Swordfish which provides a visual editor and tooling for developing with Swordfish. This is a strong use-case and showcase of Swordfish and Shoal's modularity; this module can be dropped into any Swordfish-based application to get access to developer tooling. At this time it is fairly simple and acts more as a viewer of what is going on within the engine.

## Swordfish.Integrations
This is a light API that provides utilities for working with specific tools, libraries, or resources, such as SQL and FontAwesome.

## Swordfish.Launcher
This is a simple application for launching Swordfish modules. By default, building the solution bundles the Engine, Demo, and Editor into the launcher. This is used for developer and testing, and acts as a simple example of building an application entry-point for Swordfish.

## Swordfish.Library
This is an engine-agnostic API for various types, services, and utilities used by Swordfish and some adjacent projects. This is where shared, or generally useful, APIs are placed which are not specific to the engine itself. This is useful for anything being integrated into Swordfish which does not want to take in a dependency on the engine or its own dependencies.

# External Packages & Tools
Needlefish (My binary serializer & format) https://github.com/ChaseHuegel/needlefish

Currents / CRNT (My UDP protocol) https://github.com/ChaseHuegel/Currents

Silk.NET (OpenGL and ImGui wrappers) https://github.com/dotnet/Silk.NET

ImageSharp (Image loading) https://github.com/SixLabors/ImageSharp

glTF2Loader (GLTF parsing) https://github.com/KhronosGroup/glTF-CSharp-Loader

DryIoc (DI) https://github.com/dadhi/DryIoc

Tomlet (TOML files) https://github.com/SamboyCoding/Tomlet

JoltPhysicsSharp ([Jolt Physics](https://github.com/jrouwe/JoltPhysics) C# bindings) https://github.com/amerkoleci/JoltPhysicsSharp/tree/main

msdf-atlas-gen (font atlases) https://github.com/Chlumsky/msdf-atlas-gen

# Current State: Swordfish 3
Swordfish is on V3 and underwent a full rewrite from V2 to better support modularity and extensibility, as well as decouple from tight dependencies on specific frameworks and APIs. This new version is an ongoing effort and the progress is tracked here.

## Core
- [x] Upgrade to NET 8.0
- [x] Multithreaded focus
- [x] Dependency injection focus
- [x] Localization support
- [x] Strong file parsing and import support
- [x] Command/CLI APIs
- [x] Entity Component System (ECS)
- [x] Physics
  - [x] 3D
  - [ ] 2D
- [x] UI
    - [x] 2D
    - [ ] 3D
    - [ ] File support
    - [ ] Visual editor
- [x] Renderer
  - [ ] Particles
  - [x] Lighting
    - [ ] Shadows
    - [x] Ambient
    - [x] Directional
    - [ ] Point
    - [ ] Spot
  - [x] 3D
    - [x] OpenGL
    - [ ] Vulkan
  - [x] 2D
    - [x] OpenGL
    - [ ] Vulkan
  - [x] Shaders
    - [x] Shader lang
      - [x] Parser
      - [ ] Lexer
      - [ ] Linter
      - [ ] Compiler
    - [ ] Built-in PBR
    - [x] Built-in diffuse
    - [x] OpenGL support
    - [ ] Vulkan support

## Networking
- [x] Serialization via Needlefish format
  - [x] Compressed binary format
  - [x] Schema definition lang
  - [x] Code generator
- [ ] UDP support via Currents (CRNT) protocol
- [ ] RUDP support via Currents (CRNT) protocol
- [ ] TCP support
- [ ] ECS integration

## Modding/Plugin Support
- [x] DLL modules
- [x] Module manifests & configuration
- [x] Virtual File System
- [ ] Scripting
  - [x] Parser
  - [x] Lexer
  - [x] Linter
  - [ ] Compiler

## Editor
- [ ] Project generation
- [ ] Project management
- [ ] Visual scripting
- [x] File Browser
- [x] Hierarchy
- [ ] Gizmos
- [ ] VFX editor
- [x] Inspector
  - [x] Read
  - [ ] Write

## Diagnostics
- [x] Profiler
  - [x] Data collection & logging
  - [ ] Viewer
- [x] Logging
  - [x] Log files
  - [x] Developer console
    - [ ] Command support