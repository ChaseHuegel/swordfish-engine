[![](https://img.shields.io/nuget/v/Swordfish)](https://www.nuget.org/packages/Swordfish/)
[![](https://img.shields.io/nuget/v/Swordfish.Engine?label=Engine)](https://www.nuget.org/packages/Swordfish.Engine/)
[![](https://img.shields.io/nuget/v/Swordfish.Library?label=Library)](https://www.nuget.org/packages/Swordfish.Library/)
[![](https://img.shields.io/nuget/v/Swordfish.Integrations?label=Integrations)](https://www.nuget.org/packages/Swordfish.Integrations/)
[![](https://img.shields.io/nuget/v/Needlefish?label=Needlefish)](https://www.nuget.org/packages/Needlefish/)

# Using
Silk.NET (OpenGL and ImGui wrappers) https://github.com/dotnet/Silk.NET

ImageSharp (Image loading) https://github.com/SixLabors/ImageSharp

glTF2Loader (GLTF parsing) https://github.com/KhronosGroup/glTF-CSharp-Loader

MicroResolver (DI) https://github.com/neuecc/MicroResolver

Tomlet (TOML files) https://github.com/SamboyCoding/Tomlet

Needlefish (My binary serializer) https://github.com/ChaseHuegel/needlefish

# Current State
The engine is undergoing a full rewrite to accomplish the follow:

- Update to .NET 7.0
- Create an editor tool.
- Switch to Silk.NET as the OpenGL wrapper.
- Implement a full UI API.
- Implement a full networking API.
- Introduce extensibility through proper mod and plugin support.
- Make use of dependency injection. Rewriting the renderer resulted in rewriting the engine due to tight dependencies on the old renderer.

# Archive

<details>
<summary>11/11/2021</summary>

----------------------------------
The latest updates have been smaller due to lack of time but resulted in some great fixes and progress that sets up for future changes. The big one is animation! Currently the component is primitive and infinitely loops. I'm holding out on greater functionality while I'm working on a GLTF importer for 3D keyframe animation, which will require a deeper controller. Additionally I've got plans for optimizing physics further and expanding its functionality. (i.e. parenting / compound colliders). Lastly I need to work on batching draw calls (Particle systems as a testbed??)

- Return of the cube rain! Quite intensive on collision detection, and not friendly to draw calls.
- 2D animation (TextureAnimationComponent)
- Image2D for GUI elements
- Default GUI elements for keys
- Small QoL usage change for ComponentSystem
- Physics thread has a performance watchdog and will accumulate updates instead of lagging (configurable, see PhysicsContext.cs)
- Fix: Collisions are now reliable and wont be missed.
- Fix: Thread.Abort() was unreliable and just plain bad practice. Using my own method to stop TheadWorkers now.
- Fix: Threads were originally uncapped (doh!) which lead to unnecessarily high CPU usage. Now they have a configurable rate (or uncapped!). My Ryzen 5 3600X has gone from ~25% while idling in the demo to ~1% usage

https://user-images.githubusercontent.com/14932139/141404519-0682a667-bf3c-42b1-8f84-61e5773f26fa.mp4

----------------------------------
</details>

<details>
<summary>8/21/2021</summary>
The biggest focus since the last update has been on the rendering side of things. The engine now supports rendering meshes, a custom written OBJ importer/exporter, transparency sorting and alpha blending, PBR and phong shading, point lights, billboards, screenshots, and post-processing with builtin dithering and gamma correction. The pipeline still has a ways to go and is low priority in favor of functionality right now but its nice to have brought the visuals up a notch. One clip includes billboards, meshes, PBR, HDR, dithering, gamma correction, and eye adaption (sped up for show). The second has no HDR shading or dithering for comparison.


https://user-images.githubusercontent.com/14932139/134615032-c38210ed-c30e-4866-a9a5-f9784e867b6f.mp4

https://user-images.githubusercontent.com/14932139/130310065-02ccc64b-377e-4b59-86ce-cb77f3490e78.mp4
</details>

<details>
<summary>7/2/2021</summary>

----------------------------------
Physics and collision has been implemented and runs on its own thread. There is more to be fixed and added, but the fundamentals are there. The physics engine currently uses only sphere colliders. More shapes will be supported, but the it is intended for the majority of volumes being made up of spheres. This is designed around the ideas of point clouds and Sphere Trees. This will save performance on collision checks and response by simplifying calculations and removing rotation from the equation, and at the same time allow for performance-friendly concave shapes and dynamic volumes (i.e. moving voxels)

In the clip below, red is a collision and blue is a broadphase hit, aka "These might be colliding..."

https://user-images.githubusercontent.com/14932139/124311806-4eb5f380-db3c-11eb-920c-42e9012c5b99.mp4

----------------------------------
</details>

<details>
<summary>6/14/2021</summary>

----------------------------------
Multithreading and a simple profiler! ECS runs on its own thread, giving a major performance increase. Also a rain demo to test a large amounts of entities acted on by 3 systems (Render, Gravity, Rotate). In particular this stress tests entity creation and destruction stability, reliability, and performance. There is a relability issue to be fixed (recycled entities can overlap create-destroy calls). Create-destroy calls are also expensive due to updating ComponentSystem caches being unoptimized, however multithreading has given a net-positive to performance by spreading those calls between threads.

https://user-images.githubusercontent.com/14932139/121935331-512fe500-cd16-11eb-9daa-636be21132cf.mp4

----------------------------------
</details>

<details>
<summary>6/12/2021</summary>

----------------------------------
First ECS implementation. Fully functional with room for improvement and some architecture work left.

----------------------------------
</details>

<details>
<summary>6/2/2021</summary>

----------------------------------
Unbatched rendering, debug logging, GUI (Debug and stats), 3D fly cam, naive transforms, simple shaders, dynamic texture loading

https://user-images.githubusercontent.com/14932139/120537748-a6353800-c3b3-11eb-879c-c095c43e405d.mp4

----------------------------------
</details>