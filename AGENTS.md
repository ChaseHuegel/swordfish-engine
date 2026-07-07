# Project Overview

This repository contains the **Swordfish Engine**, a modular, C#/.NET 8-based game engine. The entire architecture is built upon **Shoal**, a custom application host that provides dependency injection, containerization, and a powerful module system that implicitly enables modding. The engine itself, `Swordfish`, is a module within the Shoal ecosystem.

The primary goal of this architecture is to create a highly extensible and decoupled engine, allowing developers to easily add, remove, or replace functionality.

## Overall Project Structure

The repository is organized into several top-level directories, each representing a core part of the Swordfish Engine ecosystem:

-   `Reef/`: The immediate-mode GUI library.
-   `Shoal/`: The application host and module loader.
-   `Shoal.Build/`: MSBuild logic for packaging Shoal modules.
-   `Swordfish/`: The core game engine module.
-   `Swordfish.ECS/`: The Entity-Component-System library.
-   `Swordfish.Launcher/`: A simple application for launching Swordfish modules.
-   `Swordfish.Demo/`: A module for experimenting and showcasing engine capabilities.
-   `Swordfish.Editor/`: A module providing visual editor tooling for Swordfish.
-   `WaywardBeyond.Client.Core/`: The client-side module for the example game.
-   `WaywardBeyond.Server.Core/`: The server-side module for the example game.
-   `WaywardBeyond.Shared.Data/`: Shared data structures for the example game.
-   `WaywardBeyond.Client.Launcher/`: The launcher application for the WaywardBeyond client.

## Key Projects & Their Roles

### 1. `Shoal` - The Foundation
- **Purpose**: Application host and module loader.
- **Capabilities**:
    - Manages the application lifecycle through `AppEngine.cs`, handling startup, shutdown, and module orchestration.
    - Provides Dependency Injection (using DryIoc) via the `DependencyInjection/` directory, allowing for flexible and testable code.
    - Loads and containerizes modules (DLLs) using the logic in `Modularity/`, enabling a highly extensible and mod-friendly architecture.
    - Supports C# scripting for dynamic behavior and modding.
    - Offers command-line argument parsing and globalization features.
- **Key Files**:
    - `Shoal/AppEngine.cs`: The core application lifecycle manager.
    - `Shoal/Modularity/`: Contains the module loading and containerization logic, including `ModuleManifest.cs` for defining modules and `ModulesLoader.cs` for loading them.
    - `Shoal/DependencyInjection/`: Contains the dependency injection services.
    - `Shoal/CommandLine/`: Handles command-line argument parsing.
    - `Shoal/Globalization/`: Provides localization and globalization support.
    - `Shoal.Build/` (Provides MSBuild logic for packaging modules)

### 2. `Swordfish` - The Game Engine
- **Purpose**: The core engine module that provides all visual and game-related functionality.
- **Capabilities**:
    - **Rendering**: OpenGL via Silk.NET in the `Graphics/` directory, handling 2D and 3D rendering, shaders, and lighting.
    - **ECS**: Custom systems built upon `Swordfish.ECS` are in `ECS/`, managing game object data and logic.
    - **Input**: Handling for user input is in `Input/`, supporting various input devices.
    - **Physics**: Jolt physics integration is located in the `Physics/` directory, providing robust 3D physics simulation.
    - **UI**: Services for the user interface are in `UI/`, built on top of the `Reef` library.
    - **Audio**: Manages sound and music via the `Audio/` directory.
    - **Diagnostics**: Includes profiling and logging tools in `Diagnostics/`.
    - **Utilities**: Provides various utility functions and types in `Util/` and `Types/`.
- **Key Files**:
    - `Swordfish/SwordfishEngine.cs`: The main engine class.
    - `Swordfish/Graphics/`: Contains all rendering logic.
    - `Swordfish/Physics/`: Contains the physics integration.
    - `Swordfish/ECS/`: Home to engine-specific ECS systems.
    - `Swordfish/Input/`: Handles user input.
    - `Swordfish/UI/`: Contains UI services.
    - `Swordfish/Audio/`: Manages audio playback.
    - `Swordfish/Diagnostics/`: Provides debugging and performance tools.
    - `Swordfish/manifest.toml`: Defines the Swordfish module.

### 3. `Swordfish.ECS` - Data-Oriented Core
- **Purpose**: A zero-dependency, struct-based Entity-Component-System library.
- **Capabilities**: Provides the core data structure for game objects and their properties, focusing on performance and cache efficiency.
    - **Entities**: Represented by `Entity.cs`, serving as unique identifiers for game objects.
    - **Components**: Defined by `IDataComponent.cs`, these are plain data structures attached to entities. Stored efficiently in `DataStore.cs` and `ChunkedStore.cs`.
    - **Systems**: Logic that operates on entities and components, implemented in `EntitySystem.cs`, defining game behavior.
    - **World**: Manages the overall ECS state, including entities, components, and systems, in `World.cs`.
    - **Chunking**: `Chunk.cs` and `ChunkedStore.cs` implement a data-oriented design where components are stored in contiguous memory blocks for efficient iteration and processing.
- **Key Files**:
    - `Swordfish.ECS/Entity.cs`: Represents a unique game object.
    - `Swordfish.ECS/IDataComponent.cs`: Interface for all ECS components.
    - `Swordfish.ECS/EntitySystem.cs`: Base class for systems that process entities.
    - `Swordfish.ECS/World.cs`: Manages the collection of entities, components, and systems.
    - `Swordfish.ECS/Chunk.cs`: Represents a block of memory for storing components.
    - `Swordfish.ECS/DataStore.cs`, `Swordfish.ECS/ChunkedStore.cs`: Implementations for storing component data.

### 4. `Reef` - UI Library
- **Purpose**: A renderer-agnostic, immediate-mode GUI library.
- **Note**: This is an alpha API intended to replace the current Dear ImGui implementation.
- **Architecture**:
    - **Declarative UI**: The `UIBuilder.cs` provides a fluent API for defining UI elements in code.
    - **Constraint-Based Layout**: The system in `UI/` and `Constraints/` allows for flexible and responsive layouts. Elements can be sized and positioned using rules like `Fill`, `Fit`, and `Fixed`.
    - **Renderer-Agnostic**: `Reef` does not render directly. It generates `RenderCommand`s that a `PixelRenderer` implementation (provided by the engine) consumes.
    - **Advanced Text Rendering**: It uses Multi-channel Signed Distance Fields (MSDF) for high-quality, scalable text, with the core logic in `MSDF/` and `Text/`.
- **Key Files**:
    - `Reef/UIBuilder.cs`: The entry point for creating UI.
    - `Reef/UI/Element.cs`: The base for all UI components.
    - `Reef/Constraints/`: Contains the different layout constraint types.
    - `Reef/PixelRenderer.cs`: The interface that the rendering engine must implement.

### 5. `WaywardBeyond` - The Game
- **Purpose**: A game built using the Swordfish engine, serving as a real-world example and test case for engine features.
- **Architecture**: A client-server model.
    - `WaywardBeyond.Client.Core/`: The main client application module. This contains the core gameplay loop, rendering, UI, player controls, and asset handling. Key folders include `Systems/` for ECS logic, `Graphics/` for rendering, and `Player/` for player-specific code.
    - `WaywardBeyond.Server.Core/`: The server application module. It manages game state, networking, and data persistence. Key folders include `Networking/` and `Streaming/`.
    - `WaywardBeyond.Shared.Data/`: A project containing shared data structures and storage logic (e.g., `ICharacterStorage`) used by both client and server to ensure consistency.
- **Key Files**:
    - `WaywardBeyond.Client.Core/Entry.cs`: The entry point for the client module.
    - `WaywardBeyond.Client.Core/manifest.toml`: The Shoal manifest for the client module.
    - `WaywardBeyond.Server.Core/`: Contains server-side logic.
    - `WaywardBeyond.Shared.Data/`: Contains shared data models and persistence logic.
    - `WaywardBeyond.Client.Launcher/Program.cs`: The entry point for the client application, responsible for loading WaywardBeyond modules.

## Code Style and Practices

This section outlines the general code style and practices to adhere to when contributing to the Swordfish Engine and its associated projects. The overarching goal is to balance performance with maintainability, prioritizing performance in critical paths.

-   **Performance and Allocations**:
    -   Keep memory allocations to a minimum, especially in frequently executed code paths (hot paths).
    -   Hot paths (e.g., rendering, physics updates) must be optimized for performance, even if it means a slight reduction in maintainability or code cleanliness.
    -   For non-critical code paths, maintainability and readability should take precedence over micro-optimizations.

-   **Structs vs. Classes**:
    -   **Structs should be the default choice** for new types unless there is a clear and justified need for a class.
    -   This is especially true for pure data types (e.g., components in ECS, mathematical vectors, configuration settings) to minimize heap allocations and leverage stack allocation and value semantics.
    -   Use classes when reference semantics are required, for large mutable objects, or when inheritance/polymorphism is essential.

-   **Dependency Injection (DI)**:
    -   Dependency Injection is the default pattern for managing dependencies throughout the engine and game.
    -   Utilize the DI container provided by `Shoal` to register and resolve services.
    -   Aim for loose coupling and testability by injecting interfaces rather than concrete implementations where appropriate.

-   **ECS-First Development**:
    -   The Swordfish Engine is built around the Entity-Component-System (ECS) architectural pattern.
    -   New features and game logic should be designed with ECS principles in mind, leveraging entities, components (structs), and systems for data and behavior management.
    -   Avoid traditional object-oriented hierarchies for game objects; instead, compose functionality through components.

-   **Data-Oriented Design (DOD)**:
    -   Code should primarily follow a data-oriented design approach rather than object-oriented programming (OOP).
    -   Focus on organizing data for efficient processing (e.g., contiguous memory, cache locality) and writing systems that operate on this data.
    -   Object-oriented approaches should only be used when there is a strong, well-reasoned justification that they provide a superior solution for a specific problem.

-   **Modularity and the Shoal Ecosystem**:
    -   The Swordfish Engine is fundamentally modular, built upon the `Shoal` application host. This means the engine itself, games built with it, and any extensions are all treated as modules.
    -   **Module Definition**: Each module is defined by a `manifest.toml` file (e.g., `Swordfish/manifest.toml`, `WaywardBeyond.Client.Core/manifest.toml`), which specifies its ID, name, description, and the assemblies it contains.
    -   **Module Loading**: `Shoal`'s `Modularity/ModulesLoader.cs` handles the discovery, loading, and orchestration of these modules at runtime.
    -   **Extensibility and Modding**: This modular architecture is critical for extensibility. New features, game content, or even engine modifications can be introduced as separate modules, allowing for seamless integration and robust modding capabilities.
    -   **Coding Practices**: When developing new features or projects, always consider how they will integrate as modules within the Shoal ecosystem. This involves:
        -   Defining clear module boundaries.
        -   Utilizing Dependency Injection for inter-module communication rather than direct references.
        -   Creating and maintaining `manifest.toml` files for all new modules.
        -   Understanding that applications (like `Swordfish.Launcher` or `WaywardBeyond.Client.Launcher`) are essentially orchestrators of these modules.

## Agents

This document outlines the various agents at play in this game development project. These agents are designed to automate tasks, ensure quality, and streamline the development pipeline.

## Asset Pipeline Agent

### Overview
The Asset Pipeline Agent is responsible for processing, validating, and integrating raw art and audio assets into the game engine.

#### Capabilities
- Monitors source asset directories (e.g., for `.psd`, `.fbx`, `.wav` files).
- Automatically processes and converts assets into engine-ready formats (e.g., texture compression, model optimization).
- Applies predefined import settings based on folder structure or file naming conventions.
- Validates assets against project standards (e.g., texture dimensions, poly count, naming).
- Generates or updates prefabs from imported models.

## Build Agent

### Overview
The Build Agent automates the compilation, packaging, and deployment of the game for various target platforms.

#### Capabilities
- Compiles C# scripts and project resources into a playable build.
- Manages platform-specific build configurations (Windows, macOS, Linux, Consoles, Mobile).
- Integrates with CI/CD services like GitHub Actions or Jenkins.
- Automatically increments build versions.
- Deploys builds to services like Steam, Itch.io, or internal testing servers.

## QA Agent

### Overview
The QA Agent runs automated tests and analysis to ensure the stability, performance, and quality of the game.

#### Capabilities
- Executes unit and integration tests for core C# gameplay systems.
- Runs automated gameplay simulations to identify critical bugs, crashes, or dead-ends.
- Captures and reports performance metrics (e.g., FPS, memory usage, load times).
- Performs scene-by-scene visual regression tests to detect rendering artifacts.
- Generates detailed bug reports with logs, screenshots, and system specifications.

### Testing Strategy
The project utilizes a multi-layered testing strategy:
-   **Unit Tests**: Located in `Reef.Tests/`, `Swordfish.Tests/`, and `WaywardBeyond.Client.Core.Tests/`. These focus on individual components and methods to ensure their correctness in isolation.
-   **Integration Tests**: While not explicitly separated into dedicated projects, integration tests are crucial for verifying interactions between different systems (e.g., ECS systems interacting with rendering). These often reside within the existing test projects.
-   **Automated Gameplay Simulations**: For `WaywardBeyond`, automated simulations can be developed to test game logic, AI behavior, and overall stability under various scenarios.
-   **Performance Testing**: Performance metrics (FPS, memory, load times) are captured to identify bottlenecks and regressions.
-   **Visual Regression Tests**: Essential for the `Swordfish` engine and `Reef` UI library to detect unintended visual changes.

Agents should aim to write unit tests for new features and bug fixes, and consider integration or simulation tests for complex interactions. All tests can be executed using `dotnet test` from the command line within the respective test project directories.

## Documentation Agent

### Overview
The Documentation Agent scans the C# codebase and generates up-to-date project documentation.

#### Capabilities
- Parses XML documentation comments from C# scripts.
- Generates HTML or Markdown documentation for classes, methods, and properties.
- Automatically updates documentation as part of the CI/CD pipeline.
