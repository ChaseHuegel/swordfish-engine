using System.Numerics;
using Swordfish.Graphics.Particles;
using Swordfish.Library.Events;
using Swordfish.Library.Util;
using WaywardBeyond.Bricks;
using WaywardBeyond.Client.Events;
using WaywardBeyond.Gameplay.Interactions;

namespace WaywardBeyond.Client.Services;

/// <summary>Spawns color splatter or solvent strip bursts when a brick face's color paint changes.</summary>
internal sealed class PaintFaceParticleListener(
    in IParticleService particleService,
    IPaintDatabase? paintDatabase = null
) : IEventProcessor<PaintFaceEvent>
{
    private const string COAT_EFFECT_ID = "paint_sprayer_coat";
    private const string SOLVENT_COAT_EFFECT_ID = "paint_sprayer_solvent_coat";
    private const int CLEAR_SOLVENT_BURST_COUNT = 10;
    private const int CLEAR_FLECK_BURST_COUNT = 10;

    private readonly IParticleService _particleService = particleService;
    private readonly IPaintDatabase? _paintDatabase = paintDatabase;

    /// <inheritdoc/>
    public Result<EventBehavior> ProcessEvent(object sender, PaintFaceEvent e)
    {
        if (e.Mode != PaintKind.Color ||
            !ToolAimGeometry.IsFinite(in e.WorldPosition) ||
            !ToolAimGeometry.IsFinite(in e.Normal))
        {
            return Result<EventBehavior>.FromSuccess(EventBehavior.Continue);
        }

        Vector3 safeNormal = e.Normal.LengthSquared() > 1e-6f ? Vector3.Normalize(e.Normal) : Vector3.UnitY;
        Vector3 spawnPos = e.WorldPosition + safeNormal * ToolAimGeometry.IMPACT_EMITTER_OFFSET;
        Paint? colorPaint = ResolveColorPaint(e.PaintId);

        if (e.Action == PaintFaceAction.Apply)
        {
            uint tintRgba = colorPaint != null ? ParticleEffect.PackTint(in colorPaint.Color) : 0u;
            _particleService.EmitBurst(COAT_EFFECT_ID, spawnPos, safeNormal, count: null, speedScale: 1f, sizeScale: 1f, tintRgba: tintRgba);
            return Result<EventBehavior>.FromSuccess(EventBehavior.Continue);
        }

        if (e.Action != PaintFaceAction.Clear)
        {
            return Result<EventBehavior>.FromSuccess(EventBehavior.Continue);
        }

        _particleService.EmitBurst(SOLVENT_COAT_EFFECT_ID, spawnPos, safeNormal, count: CLEAR_SOLVENT_BURST_COUNT, speedScale: 1f, sizeScale: 1f, tintRgba: 0u);

        if (colorPaint != null)
        {
            uint fleckTint = ParticleEffect.PackTint(in colorPaint.Color);
            _particleService.EmitBurst(COAT_EFFECT_ID, spawnPos, safeNormal, count: CLEAR_FLECK_BURST_COUNT, speedScale: 1f, sizeScale: 1f, tintRgba: fleckTint);
        }

        return Result<EventBehavior>.FromSuccess(EventBehavior.Continue);
    }

    private Paint? ResolveColorPaint(ushort paintId)
    {
        if (_paintDatabase == null || paintId == 0)
        {
            return null;
        }

        Result<Paint> result = _paintDatabase.Get(paintId);
        return result.Success && result.Value.Kind == PaintKind.Color ? result.Value : null;
    }
}
