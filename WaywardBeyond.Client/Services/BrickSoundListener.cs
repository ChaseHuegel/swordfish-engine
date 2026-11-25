using Swordfish.Library.Events;
using Swordfish.Library.Util;
using WaywardBeyond.Bricks;
using WaywardBeyond.Client.Events;

namespace WaywardBeyond.Client.Services;

/// <summary>Plays a brick's declared sound tags when a brick is placed or broken.</summary>
internal sealed class BrickSoundListener(
    in IBrickDatabase brickDatabase,
    in SoundEffectService soundEffectService
) : IEventProcessor<BrickEvent>
{
    private readonly IBrickDatabase _brickDatabase = brickDatabase;
    private readonly SoundEffectService _soundEffectService = soundEffectService;

    /// <inheritdoc/>
    public Result<EventBehavior> ProcessEvent(object sender, BrickEvent e)
    {
        if (e.Kind != BrickEventKind.Place && e.Kind != BrickEventKind.Break)
        {
            return Result<EventBehavior>.FromSuccess(EventBehavior.Continue);
        }

        if (e.BrickID != 0)
        {
            Result<Brick> brick = _brickDatabase.Get(e.BrickID);
            if (brick.Success)
            {
                _soundEffectService.PlayEffect(brick.Value, e.Kind);
            }
        }

        return Result<EventBehavior>.FromSuccess(EventBehavior.Continue);
    }
}
