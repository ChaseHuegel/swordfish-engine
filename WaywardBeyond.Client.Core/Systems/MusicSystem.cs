using System.IO;
using System.Linq;
using Reef;
using Swordfish.Audio;
using Swordfish.ECS;
using Swordfish.Graphics;
using Swordfish.IO;
using Swordfish.Library.IO;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Core.UI;

namespace WaywardBeyond.Client.Core.Systems;

internal sealed class MusicSystem : IEntitySystem, IDebugOverlay
{
    private readonly AudioChannelSystem _audioChannelSystem;
    private readonly NotificationService _notificationService;
    
    private readonly Randomizer _randomizer;
    private readonly string[] _titleTracks;
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

        PathInfo titleMusicPath = AssetPaths.Audio.At("music/title/");
        _titleTracks = vfs.GetFiles(titleMusicPath, SearchOption.TopDirectoryOnly)
            .Select(pathInfo => $"music/title/{pathInfo.GetFileName()}")
            .ToArray();
        
        PathInfo backgroundMusicPath = AssetPaths.Audio.At("music/background/");
        _backgroundTracks = vfs.GetFiles(backgroundMusicPath, SearchOption.TopDirectoryOnly)
            .Select(pathInfo => $"music/background/{pathInfo.GetFileName()}")
            .ToArray();
    }
    
    public void Tick(float delta, DataStore store)
    {
        if (_entity == null)
        {
            if (!_audioChannelSystem.TryGetChannelEntity("music", out Uuid channel))
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
        
        QueryEntityAction queryEntity = new() { Owner = this };
        store.QueryRef<AudioPlayer, AudioSource, QueryEntityAction>(_entity.Value, delta, ref queryEntity);
    }

    private struct QueryEntityAction : IForEachRef<AudioPlayer, AudioSource>
    {
        public MusicSystem Owner;

        public void Execute(float delta, DataStore store, int entity, ref Ref<AudioPlayer> audioPlayer, ref Ref<AudioSource> audioSource)
        {
            if (audioPlayer.Read.State != PlayerState.Stop)
            {
                return;
            }

            //  If playback has finished, start a timer until the next track
            if (Owner._nextTrackTimer == null)
            {
                //  Tracks will play quicker when not in-game
                Owner._nextTrackTimer = WaywardBeyond.IsInGame() ? Owner._randomizer.NextInt(20, 60) : Owner._randomizer.NextInt(2, 6);
                return;
            }

            //  Countdown the timer
            Owner._nextTrackTimer -= delta;
            if (Owner._nextTrackTimer > 0f)
            {
                return;
            }

            //  Timer has elapsed, remove it then start the next track
            Owner._nextTrackTimer = null;

            audioSource.Write.ID = WaywardBeyond.GameState == GameState.MainMenu ? Owner._randomizer.Select(Owner._titleTracks) : Owner._randomizer.Select(Owner._backgroundTracks);
            audioPlayer.Write.State = PlayerState.Play;
            Owner._notificationService.Push(new Notification($"Track: {System.IO.Path.GetFileNameWithoutExtension(audioSource.Read.ID)}"));
        }
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