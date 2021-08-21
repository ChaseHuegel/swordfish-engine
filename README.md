## Using
OpenTK (OpenGL C# wrapper) https://github.com/opentk/opentk

ImGui.NET (ImGui C# wrapper) https://github.com/mellinoe/ImGui.NET

Tomlet (TOML file format for C#) https://github.com/SamboyCoding/Tomlet

----------------------------------

## Updates

### 8/21/2021
The biggest focus since the last update has been on the rendering side of things. The engine now supports rendering meshes, a custom written OBJ importer/exporter, transparency sorting and alpha blending, PBR and phong shading, point lights, billboards, screenshots, and post-processing with builtin dithering and gamma correction. The pipeline still has a ways to go and is low priority in favor of functionality right now but its nice to have brought the visuals up a notch.  

https://user-images.githubusercontent.com/14932139/130310065-02ccc64b-377e-4b59-86ce-cb77f3490e78.mp4


### 7/2/2021
Physics and collision has been implemented and runs on its own thread. There is more to be fixed and added, but the fundamentals are there. The physics engine currently uses only sphere colliders. More shapes will be supported, but the it is intended for the majority of volumes being made up of spheres. This is designed around the ideas of point clouds and Sphere Trees. This will save performance on collision checks and response by simplifying calculations and removing rotation from the equation, and at the same time allow for performance-friendly concave shapes and dynamic volumes (i.e. moving voxels)

In the clip below, red is a collision and blue is a broadphase hit, aka "These might be colliding..."

https://user-images.githubusercontent.com/14932139/124311806-4eb5f380-db3c-11eb-920c-42e9012c5b99.mp4


### 6/14/2021
Multithreading and a simple profiler! ECS runs on its own thread, giving a major performance increase. Also a rain demo to test a large amounts of entities acted on by 3 systems (Render, Gravity, Rotate). In particular this stress tests entity creation and destruction stability, reliability, and performance. There is a relability issue to be fixed (recycled entities can overlap create-destroy calls). Create-destroy calls are also expensive due to updating ComponentSystem caches being unoptimized, however multithreading has given a net-positive to performance by spreading those calls between threads.

https://user-images.githubusercontent.com/14932139/121935331-512fe500-cd16-11eb-9daa-636be21132cf.mp4


### 6/12/2021
First ECS implementation. Fully functional with room for improvement and some architecture work left.


### 6/2/2021
Unbatched rendering, debug logging, GUI (Debug and stats), 3D fly cam, naive transforms, simple shaders, dynamic texture loading

https://user-images.githubusercontent.com/14932139/120537748-a6353800-c3b3-11eb-879c-c095c43e405d.mp4

