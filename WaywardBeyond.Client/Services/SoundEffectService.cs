using System.Collections.Generic;
using Krill;
using Swordfish.Audio;
using Swordfish.Library.Util;
using WaywardBeyond.Bricks;
using WaywardBeyond.Client.Systems;
using WaywardBeyond.Data.Localization;

namespace WaywardBeyond.Client.Services;

/// <summary>Plays tag-based sound effects on the client audio channels.</summary>
internal class SoundEffectService
{
    private const string EFFECTS_CHANNEL = "effects";
    private const string INTERFACE_CHANNEL = "interface";

    private readonly AudioChannelSystem _audioChannelSystem;
    private readonly LocalizedTags? _invariantTags;
    private readonly Randomizer _randomizer;

    public SoundEffectService(in AudioChannelSystem audioChannelSystem, in ILocalizedTagsDatabase tagDatabase)
    {
        _audioChannelSystem = audioChannelSystem;
        _invariantTags = tagDatabase.Invariant;
        _randomizer = new Randomizer();
    }

    /// <summary>Plays the brick's declared sound for the provided brick event.</summary>
    public void PlayEffect(Brick brick, BrickEventKind eventKind)
    {
        if (!brick.Sounds.TryGetValue(eventKind, out string[]? tags))
        {
            return;
        }

        Play(EFFECTS_CHANNEL, tags);
    }

    /// <summary>Plays a random clip from the provided invariant sound tag on the interface channel.</summary>
    public virtual void PlayInterface(string tag)
    {
        Play(INTERFACE_CHANNEL, [tag]);
    }

    private void Play(string channelName, IReadOnlyList<string> tags)
    {
        if (_invariantTags == null)
        {
            return;
        }

        var clips = new List<string>();
        foreach (string tag in tags)
        {
            IReadOnlyList<string>? values = _invariantTags.GetValues(tag);
            if (values == null)
            {
                continue;
            }

            for (var i = 0; i < values.Count; i++)
            {
                if (!clips.Contains(values[i]))
                {
                    clips.Add(values[i]);
                }
            }
        }

        if (clips.Count == 0 || !_audioChannelSystem.TryGetChannelEntity(channelName, out Uuid channel))
        {
            return;
        }

        var audioSource = new AudioSource(clips[_randomizer.NextInt(clips.Count)]);
        var audioPlayer = new AudioPlayer
        {
            Volume = 1f,
            Pitch = 1f,
            Loop = false,
            PlayOnce = true,
            State = PlayerState.Play,
            ChannelEntity = channel,
        };
        _audioChannelSystem.EnqueuePlay(audioSource, audioPlayer);
    }
}
