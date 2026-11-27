# Presentation Time

One subject: accumulated time for shader uniforms and CPU-driven pulses in long sessions.

## Wrap interval

A `float` loses phase precision as elapsed time grows. `MathS.TIME_WRAP_SECONDS`
(`Swordfish.Library/Util/MathS.cs:13`) is `2π × 512` seconds, about 53.6 minutes.
The value is a whole number of 2π cycles, so `sin` and `cos` of an integer radian
frequency stay continuous across the wrap.

Both accumulators wrap with `MathS.Repeat` (`Swordfish.Library/Util/MathS.cs:27`):

- `ForwardPlusRenderingPipeline` keeps `double _elapsedTime`
  (`Swordfish/Graphics/SilkNET/OpenGL/Pipelines/ForwardPlusRenderingPipeline.cs:120`),
  adds a finite positive `delta` under the wrap (`ForwardPlusRenderingPipeline.cs:372-375`),
  and writes the wrapped value to `uTime` (`ForwardPlusRenderingPipeline.cs:1123`).
- `MiningLaserEffectSystem` wraps its `float _time` (`WaywardBeyond.Client/Systems/MiningLaserEffectSystem.cs:84-87`)
  before the beam thickness pulse (`MiningLaserEffectSystem.cs:190`) and impact light
  flicker (`MiningLaserEffectSystem.cs:210`). Both use integer radian frequencies.

A shader that uses a non-integer radian frequency, or that reads accumulate-only
values such as unbounded scroll offsets, still sees a discontinuity at the wrap.
Use an integer radian frequency for periodic animation.

## Tests that pin this

- `Swordfish.Tests/TimeWrapTests.cs` pins the interval, the wrap bound, continuity
  for the shipped `36`, `48`, `53`, and `62` rad/s frequencies, and phase accuracy
  after long elapsed time.
- `WaywardBeyond.Tests/MiningLaserEffectsTests.cs:637` pins the mining pulse after
  a wrap.
