using System.IO;
using System.Linq;
using Reef;
using Swordfish.Audio;
using Swordfish.ECS;
using Swordfish.Graphics;
using Swordfish.IO;
using Swordfish.Library.IO;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Core.Systems;
using WaywardBeyond.Client.Core.UI;
using WaywardBeyond.Client.Core.UI.Layers;

namespace WaywardBeyond.Client.Core.Services;

internal sealed class MusicSystem : IEntitySystem, IDebugOverlay
{
    private readonly AudioChannelSystem _audioChannelSystem;
    private readonly NotificationService _notificationService;
    
    private readonly Randomizer _randomizer;
    private readonly string[] _backgroundTracks;
    
    private int? _entity;
    private float? _nextTrackTimer;

    public MusicSystem(
        in AudioChannelSystem audioChannelSystem,
        in VirtualFileSystem vfs,
        in NotificationService notificationService
    ) {
        _audioChannelSystem = audioChannelSystem;
        _notificationService = notificationService;
        
        _randomizer = new Randomizer();

        PathInfo backgroundMusicPath = AssetPaths.Audio.At("music/background/");
        _backgroundTracks = vfs.GetFiles(backgroundMusicPath, SearchOption.TopDirectoryOnly)
            .Select(pathInfo => $"music/background/{pathInfo.GetFileName()}")
            .ToArray();
    }
    
    public void Tick(float delta, DataStore store)
    {
        if (_entity == null)
        {
            if (!_audioChannelSystem.TryGetChannelEntity("music", out int channel))
            {
                return;
            }
            
            var audioPlayer = new AudioPlayer
            {
                Volume = 1f,
                Pitch = 1f,
                Loop = false,
                PlayOnce = false,
                State = PlayerState.Stop,
                ChannelEntity = channel,
            };
            
            _entity = store.Alloc(audioPlayer, new AudioSource());
        }
        
        store.Query<AudioPlayer, AudioSource>(_entity.Value, delta, QueryEntity);
    }

    private void QueryEntity(float delta, DataStore store, int entity, ref AudioPlayer audioPlayer, ref AudioSource audioSource)
    {
        if (audioPlayer.State != PlayerState.Stop)
        {
            return;
        }

        //  If playback has finished, start a timer until the next track
        if (_nextTrackTimer == null)
        {
            //  Tracks will play quicker when not in-game
            _nextTrackTimer = WaywardBeyond.IsInGame() ? _randomizer.NextInt(20, 60) : _randomizer.NextInt(2, 6);
            return;
        }

        //  Countdown the timer
        _nextTrackTimer -= delta;
        if (_nextTrackTimer > 0f)
        {
            return;
        }

        //  Timer has elapsed, remove it then start the next track
        _nextTrackTimer = null;

        audioSource.ID = _randomizer.Select(_backgroundTracks);
        audioPlayer.State = PlayerState.Play;
        _notificationService.Push(new Notification($"Track: {Path.GetFileNameWithoutExtension(audioSource.ID)}"));
    }

    public bool IsVisible()
    {
        return true;
    }

    public Result RenderDebugOverlay(double delta, UIBuilder<Material> ui)
    {
        using (ui.Text($"Next track in: {_nextTrackTimer ?? 0}")) { }
        return Result.FromSuccess();
    }
}