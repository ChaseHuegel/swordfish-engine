using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging.Abstractions;
using Swordfish.IO;
using Swordfish.Library.IO;
using Swordfish.Library.Serialization.Toml;
using WaywardBeyond.Bricks;
using WaywardBeyond.Client.UI;
using WaywardBeyond.Data.Localization;
using Xunit;

namespace WaywardBeyond.Tests;

/// <summary>Cross-checks the shipped brick and interface sound tags against the invariant tag lists.</summary>
public class SoundTagTests
{
    [Fact]
    public void EveryShippedBrickDeclaresResolvableSounds()
    {
        Fixture fixture = Load();

        foreach (Brick brick in fixture.Bricks.Get(_ => true))
        {
            Assert.True(brick.Sounds.ContainsKey(BrickSoundEvent.Place), $"Brick \"{brick.ID}\" declares no place sound.");
            Assert.True(brick.Sounds.ContainsKey(BrickSoundEvent.Break), $"Brick \"{brick.ID}\" declares no break sound.");

            foreach ((BrickSoundEvent soundEvent, string[] tags) in brick.Sounds)
            {
                Assert.NotEmpty(tags);
                foreach (string tag in tags)
                {
                    AssertResolves(fixture, tag, $"brick \"{brick.ID}\" {soundEvent}");
                }
            }
        }
    }

    [Fact]
    public void EveryInterfaceSoundTagResolvesToAudioClips()
    {
        Fixture fixture = Load();

        foreach (WidgetInteraction.InterfaceSound sound in Enum.GetValues<WidgetInteraction.InterfaceSound>())
        {
            AssertResolves(fixture, WidgetInteraction.GetSoundTag(sound), $"interface sound {sound}");
        }
    }

    private static void AssertResolves(Fixture fixture, string tag, string owner)
    {
        IReadOnlyList<string>? resolved = fixture.Tags.GetValues(tag);
        IReadOnlyList<string> clips = resolved ?? throw new InvalidOperationException($"No audio clips resolve for {owner}.");
        Assert.NotEmpty(clips);

        foreach (string clip in clips)
        {
            Assert.True(fixture.Vfs.FileExists(AssetPaths.Audio.At(clip)), $"Missing audio clip \"{clip}\" for {owner}.");
        }
    }

    private static Fixture Load()
    {
        var vfs = new VirtualFileSystem();
        vfs.Mount(new PathInfo("assets"));

        IFileParser[] parsers =
        [
            new TomlParser<BrickDefinitions>(),
            new LocalizedTagDefinitionParser(),
        ];
        var parser = new VirtualFileParseService(parsers, vfs);

        var bricks = new BrickDatabase(NullLogger<BrickDatabase>.Instance, parser, vfs);
        var tags = new LocalizedTagsDatabase(NullLogger<LocalizedTagsDatabase>.Instance, parser, vfs);
        LocalizedTags invariant = tags.Invariant ?? throw new InvalidOperationException("No invariant tags were loaded.");
        return new Fixture(vfs, bricks, invariant);
    }

    private readonly record struct Fixture(VirtualFileSystem Vfs, BrickDatabase Bricks, LocalizedTags Tags);
}
