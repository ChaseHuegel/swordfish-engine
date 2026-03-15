using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Shoal.Modularity;
using Swordfish.Audio;
using Swordfish.IO;
using Swordfish.Library.IO;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Core.Configuration;
using WaywardBeyond.Client.Core.UI;
using WaywardBeyond.Client.Core.UI.Layers;

namespace WaywardBeyond.Client.Core.Services;

internal sealed class MusicService : IEntryPoint
{
    private readonly IAudioService _audioService;
    private readonly VolumeSettings _volumeSettings;
    private readonly NotificationService _notificationService;
    private readonly string[] _backgroundTracks;

    public MusicService(
        in IAudioService audioService,
        in VolumeSettings volumeSettings,
        in VirtualFileSystem vfs,
        in NotificationService notificationService
    ) {
        _audioService = audioService;
        _volumeSettings = volumeSettings;
        _notificationService = notificationService;

        PathInfo backgroundMusicPath = AssetPaths.Audio.At("music/").At("background/");
        _backgroundTracks = vfs.GetFiles(backgroundMusicPath, SearchOption.TopDirectoryOnly)
            .Select(pathInfo => pathInfo.GetFileName())
            .ToArray();
    }

    public void Run()
    {
        Task.Run(PlayMusicAsync);
    }

    private async Task PlayMusicAsync()
    {
        var randomizer = new Randomizer();
        while (true)
        {
            string track = randomizer.Select(_backgroundTracks);
            _notificationService.Push(new Notification($"Music: {Path.GetFileNameWithoutExtension(track)}"));
            _audioService.Play(id: $"music/background/{track}", _volumeSettings.MixMusic(), block: true);

            await Task.Delay(randomizer.NextInt(10_000));
        }
        // ReSharper disable once FunctionNeverReturns
    }
}