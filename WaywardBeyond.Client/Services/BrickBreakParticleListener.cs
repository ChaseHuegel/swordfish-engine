using System.Numerics;
using Swordfish.Graphics.Particles;
using Swordfish.Library.Events;
using Swordfish.Library.Util;
using WaywardBeyond.Bricks;
using WaywardBeyond.Client.Events;

namespace WaywardBeyond.Client.Services;

/// <summary>Spawns a mining impact spark burst when a brick is broken in world space.</summary>
internal sealed class BrickBreakParticleListener(in IParticleService particleService) : IEventProcessor<BrickEvent>
{
    private const string BREAK_EFFECT_ID = "mining_laser_break";

    private readonly IParticleService _particleService = particleService;

    /// <inheritdoc/>
    public Result<EventBehavior> ProcessEvent(object sender, BrickEvent e)
    {
        if (e.BrickID == 0 || e.Kind != BrickEventKind.Break || !e.WorldPosition.HasValue)
        {
            return Result<EventBehavior>.FromSuccess(EventBehavior.Continue);
        }

        _particleService.EmitBurst(BREAK_EFFECT_ID, e.WorldPosition.Value, e.Normal ?? Vector3.UnitY);
        return Result<EventBehavior>.FromSuccess(EventBehavior.Continue);
    }
}
