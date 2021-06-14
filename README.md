## Using
OpenTK (OpenGL C# wrapper) https://github.com/opentk/opentk

ImGui.NET (ImGui C# wrapper) https://github.com/mellinoe/ImGui.NET

----------------------------------

## Updates

### 6/14/2021
Multithreading and a simple profiler! ECS runs on its own thread, giving a major performance increase. Also a rain demo to test a large amounts of entities acted on by 3 systems (Render, Gravity, Rotate). In particular this stress tests entity creation and destruction stability, reliability, and performance. There is a relability issue to be fixed (recycled entities can overlap create-destroy calls). Create-destroy calls are also expensive due to updating ComponentSystem caches being unoptimized, however multithreading has given a net-positive to performance by spreading those calls between threads.

#### Delta Time
Below are timing results, delta time is the time between frames or updates to the thread. For reference, 60FPS is equivalent to 16.67ms. There are ~3k entities alive at any given time, with ~1k create and ~1k destroy calls per second. A great start!

* ~6ms on Main (single thread)
* ~1ms on Main ~2ms on ECS (dual threaded)
* Rendering accounts for ~0.1ms on Main (unbatched draw calls)
* CPU: AMD Ryzen 5 3600X
* GPU: GeForce GTX 970

https://user-images.githubusercontent.com/14932139/121935331-512fe500-cd16-11eb-9daa-636be21132cf.mp4


### 6/12/2021
First ECS implementation. Fully functional with room for improvement and some architecture work left.


### 6/2/2021
Unbatched rendering, debug logging, GUI (Debug and stats), 3D fly cam, naive transforms, simple shaders, dynamic texture loading

https://user-images.githubusercontent.com/14932139/120537748-a6353800-c3b3-11eb-879c-c095c43e405d.mp4

