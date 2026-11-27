#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Reef;
using Reef.UI;
using Swordfish.Graphics;
using Swordfish.Input;
using Swordfish.Library.Collections;
using Swordfish.Library.Globalization;
using Swordfish.Library.IO;
using Swordfish.Library.Util;
using Swordfish.Tests;
using WaywardBeyond.Client.Events;
using WaywardBeyond.Client.Saves;
using WaywardBeyond.Client.Skills;
using WaywardBeyond.Client.Systems;
using WaywardBeyond.Client.UI.Layers.Skills;
using WaywardBeyond.Data;
using WaywardBeyond.Skills;
using Xunit;
using ClientGameState = WaywardBeyond.Client.GameState;
using ClientHost = WaywardBeyond.Client.WaywardBeyond;

namespace WaywardBeyond.Tests;

/// <summary>Covers skill level and bar progress math, icon fallback and warnings, category grouping, and skill panel UI behavior.</summary>
[Collection("ClientGameState")]
public sealed class SkillsPanelTests : IDisposable
{
    public SkillsPanelTests()
    {
        ClientHost.GameState.Set(ClientGameState.Playing);
    }

    public void Dispose()
    {
        ClientHost.GameState.Set(ClientGameState.MainMenu);
    }

    [Fact]
    public void CalculateProgressMatchesCalculateLevelAndBarMathAcrossThresholds()
    {
        (SkillDatabase skills, _) = SkillDatabaseTests.CreateSharedFixture();
        Skill mining = skills.Get("mining").Value;

        long maxThreshold = mining.Levels.Values.Aggregate(0L, static (acc, req) => acc + req);
        long[] sampleTotals = [0, 30, 61, 62, 105, 166, 167, maxThreshold, maxThreshold + 1, maxThreshold + 250];

        foreach (long totalXP in sampleTotals)
        {
            LevelInfo expectedLevel = mining.CalculateLevel(totalXP);
            SkillProgress progress = mining.CalculateProgress(totalXP);

            Assert.Equal(expectedLevel.Level, progress.Level);
            Assert.Equal(expectedLevel.XPIntoLevel, progress.XPIntoLevel);
            Assert.Equal(totalXP, progress.TotalXP);

            if (mining.Levels.TryGetValue(expectedLevel.Level + 1, out int nextRequired))
            {
                Assert.False(progress.IsMaxLevel);
                Assert.Equal(nextRequired, progress.NextLevelXP);
                Assert.Equal((float)expectedLevel.XPIntoLevel / nextRequired, progress.Progress, precision: 5);
            }
            else
            {
                Assert.True(progress.IsMaxLevel);
                Assert.Equal(0, progress.NextLevelXP);
                Assert.Equal(1f, progress.Progress, precision: 5);
            }
        }
    }

    [Fact]
    public void SkillIconDatabaseResolvesTextureFallsBackToUnknownAndLogsWarningOnce()
    {
        var unknownTexture = new Texture(SkillIconDatabase.UNKNOWN_ICON, new byte[4], 1, 1, mipmaps: false);
        var miningTexture = new Texture("skills/mining.png", new byte[4], 1, 1, mipmaps: false);
        var textures = new TestTextureDatabase(new Dictionary<string, Texture>
        {
            [SkillIconDatabase.UNKNOWN_ICON] = unknownTexture,
            ["skills/mining.png"] = miningTexture,
        });

        var logger = new CapturingLogger<SkillIconDatabase>();
        using var uiShader = new Shader("ui", []);
        using var iconDb = new SkillIconDatabase(logger, uiShader, textures);

        Skill validSkill = CreateSkill("mining", "skill.name.mining", "skill.category.gathering", "skills/mining.png");
        Skill unknownPathSkill = CreateSkill("mystery", "skill.name.mystery", "skill.category.misc", "skills/missing.png");
        Skill nullIconSkill = CreateSkill("blank", "skill.name.blank", "skill.category.misc", icon: null);

        Material validMaterial = iconDb.Get(validSkill);
        Material validMaterialAgain = iconDb.Get(validSkill);
        Material fallbackMaterial = iconDb.Get(unknownPathSkill);
        Material fallbackMaterialAgain = iconDb.Get(unknownPathSkill);
        Material nullIconMaterial = iconDb.Get(nullIconSkill);

        Assert.Same(miningTexture, validMaterial.Textures[0]);
        Assert.Same(validMaterial, validMaterialAgain);
        Assert.Same(unknownTexture, fallbackMaterial.Textures[0]);
        Assert.Same(fallbackMaterial, fallbackMaterialAgain);
        Assert.Same(unknownTexture, nullIconMaterial.Textures[0]);

        Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, logger.Entries[0].Level);
        Assert.Contains("skills/missing.png", logger.Entries[0].Message, StringComparison.Ordinal);
        Assert.Contains("mystery", logger.Entries[0].Message, StringComparison.Ordinal);
    }

    [Fact]
    public void PanelGroupsEverySkillByCategoryAndPinsRowStateToCalculateLevel()
    {
        (SkillDatabase skills, _) = SkillDatabaseTests.CreateSharedFixture();
        Skill mining = skills.Get("mining").Value;
        Skill building = skills.Get("building").Value;
        Skill salvaging = skills.Get("salvaging").Value;

        var unknownTexture = new Texture(SkillIconDatabase.UNKNOWN_ICON, new byte[4], 1, 1, mipmaps: false);
        var miningTexture = new Texture("skills/mining.png", new byte[4], 1, 1, mipmaps: false);
        var buildingTexture = new Texture("skills/building.png", new byte[4], 1, 1, mipmaps: false);
        var salvagingTexture = new Texture("skills/salvaging.png", new byte[4], 1, 1, mipmaps: false);
        var textures = new TestTextureDatabase(new Dictionary<string, Texture>
        {
            [SkillIconDatabase.UNKNOWN_ICON] = unknownTexture,
            ["skills/mining.png"] = miningTexture,
            ["skills/building.png"] = buildingTexture,
            ["skills/salvaging.png"] = salvagingTexture,
        });

        var character = new Character
        {
            Name = "Spacer",
            Statistics =
            [
                new Statistic("mining", 105),
                new Statistic("building", 62),
                new Statistic("salvaging", 0),
            ],
        };

        var activeSave = new ActiveCharacterSave { ActiveSave = character };
        using var harness = new PanelHarness(skills, textures, activeSave, CreateSkillLocalization());

        SkillsPanelState state = harness.Layer.BuildState();

        Assert.True(state.HasSkills);
        Assert.Equal(2, state.Categories.Count);
        Assert.Equal("skill.category.gathering", state.Categories[0].CategoryKey);
        Assert.Equal("skill.category.misc", state.Categories[1].CategoryKey);

        Assert.Equal(["mining", "salvaging"], state.Categories[0].Skills.Select(s => s.ID).ToArray());
        Assert.Equal(["building"], state.Categories[1].Skills.Select(s => s.ID).ToArray());

        SkillRowState miningRow = state.Categories[0].Skills[0];
        LevelInfo expectedMining = mining.CalculateLevel(105);
        Assert.Equal("skill.name.mining", miningRow.NameKey);
        Assert.Equal("skill.category.gathering", miningRow.CategoryKey);
        Assert.Same(miningTexture, miningRow.Icon.Textures[0]);
        Assert.Equal(expectedMining.Level, miningRow.Level);
        Assert.Equal(105, miningRow.TotalXP);
        Assert.Equal(expectedMining.XPIntoLevel, miningRow.XPIntoLevel);
        Assert.Equal(mining.Levels[expectedMining.Level + 1], miningRow.NextLevelXP);
        Assert.Equal((float)expectedMining.XPIntoLevel / mining.Levels[expectedMining.Level + 1], miningRow.Progress, precision: 5);

        SkillRowState buildingRow = state.Categories[1].Skills[0];
        LevelInfo expectedBuilding = building.CalculateLevel(62);
        Assert.Equal(expectedBuilding.Level, buildingRow.Level);
        Assert.Equal(62, buildingRow.TotalXP);
        Assert.Equal(expectedBuilding.XPIntoLevel, buildingRow.XPIntoLevel);
        Assert.Same(buildingTexture, buildingRow.Icon.Textures[0]);

        SkillRowState salvagingRow = state.Categories[0].Skills[1];
        LevelInfo expectedSalvaging = salvaging.CalculateLevel(0);
        Assert.Equal(expectedSalvaging.Level, salvagingRow.Level);
        Assert.Equal(0, salvagingRow.TotalXP);
        Assert.Equal(expectedSalvaging.XPIntoLevel, salvagingRow.XPIntoLevel);
        Assert.Equal(0f, salvagingRow.Progress, precision: 5);
        Assert.Same(salvagingTexture, salvagingRow.Icon.Textures[0]);

        Assert.Equal(expectedMining.Level + expectedBuilding.Level + expectedSalvaging.Level, state.TotalLevel);

        harness.Layer.Open();
        List<RenderCommand<Material>> commands = harness.Render();

        Assert.Contains(commands, c => c.Text == "Skills");
        Assert.Contains(commands, c => c.Text == "Gathering");
        Assert.Contains(commands, c => c.Text == "Misc");
        Assert.Contains(commands, c => c.Text == "Mining");
        Assert.Contains(commands, c => c.Text == "Building");
        Assert.Contains(commands, c => c.Text == "Salvaging");
        Assert.Contains(commands, c => c.Text == "Total level 2");
        Assert.Contains(commands, c => c.Text == "Lvl 1");
        Assert.Contains(commands, c => c.Text == "44 / 105 XP");
        Assert.Contains(commands, c => c.Text == "Total 105 XP");
        Assert.DoesNotContain(commands, c => c.Text == "Close");
        Assert.Contains(commands, c => c.RendererData != null && ReferenceEquals(c.RendererData.Textures[0], miningTexture));

        ElementBounds panelBounds = harness.GetElementBounds("Skills.Panel");
        Assert.Equal(512, panelBounds.Rect.Size.X);
        Assert.InRange(panelBounds.Rect.Size.Y, 320, 400);

        //  Live character XP update immediately updates both skill level and total level.
        character.Statistics =
        [
            new Statistic("mining", 105),
            new Statistic("building", 167),
            new Statistic("salvaging", 62),
        ];
        activeSave.ActiveSave = character;
        List<RenderCommand<Material>> updatedCommands = harness.Render(0.2d);
        Assert.Contains(updatedCommands, c => c.Text == "Total level 4");
    }

    [Fact]
    public void PanelHandlesEmptyDatabaseAndUncategorizedSkills()
    {
        var unknownTexture = new Texture(SkillIconDatabase.UNKNOWN_ICON, new byte[4], 1, 1, mipmaps: false);
        var textures = new TestTextureDatabase(new Dictionary<string, Texture>
        {
            [SkillIconDatabase.UNKNOWN_ICON] = unknownTexture,
        });

        var activeSave = new ActiveCharacterSave();
        using var emptyHarness = new PanelHarness(new ListSkillDatabase([]), textures, activeSave, CreateSkillLocalization());
        emptyHarness.Layer.Open();
        List<RenderCommand<Material>> emptyCommands = emptyHarness.Render();
        Assert.Contains(emptyCommands, c => c.Text == "No skills available");

        Skill uncategorized = CreateSkill("custom", "skill.name.custom", string.Empty, "skills/missing.png");
        using var uncategorizedHarness = new PanelHarness(new ListSkillDatabase([uncategorized]), textures, activeSave, CreateSkillLocalization());
        SkillsPanelState state = uncategorizedHarness.Layer.BuildState();

        Assert.Single(state.Categories);
        Assert.Equal(SkillsLayer.UNCATEGORIZED_CATEGORY_KEY, state.Categories[0].CategoryKey);
        Assert.Same(unknownTexture, state.Categories[0].Skills[0].Icon.Textures[0]);
        Assert.Single(uncategorizedHarness.IconLogger.Entries);
        Assert.Equal(LogLevel.Warning, uncategorizedHarness.IconLogger.Entries[0].Level);
    }

    [Fact]
    public void LayerTogglesViaShortcutBlocksInteractionAndClosesOnEscapeOrButton()
    {
        (SkillDatabase skills, _) = SkillDatabaseTests.CreateSharedFixture();
        var unknownTexture = new Texture(SkillIconDatabase.UNKNOWN_ICON, new byte[4], 1, 1, mipmaps: false);
        var textures = new TestTextureDatabase(new Dictionary<string, Texture>
        {
            [SkillIconDatabase.UNKNOWN_ICON] = unknownTexture,
        });

        using var harness = new PanelHarness(skills, textures, new ActiveCharacterSave(), CreateSkillLocalization());

        Assert.False(harness.Layer.IsVisible());
        Assert.False(harness.InteractionState.IsInteractionBlocked());

        harness.PressKey(Key.K);
        Assert.True(harness.Layer.IsVisible());
        Assert.True(harness.InteractionState.IsInteractionBlocked());

        //  Pressing K again toggles closed.
        harness.PressKey(Key.K);
        Assert.False(harness.Layer.IsVisible());
        Assert.False(harness.InteractionState.IsInteractionBlocked());

        //  Reopen and close via Back (Esc).
        harness.PressKey(Key.K);
        Assert.True(harness.Layer.IsVisible());
        harness.PressKey(Key.Esc);
        Assert.False(harness.Layer.IsVisible());
        Assert.False(harness.InteractionState.IsInteractionBlocked());

        //  Reopen and close via Close button.
        harness.PressKey(Key.K);
        Assert.True(harness.Layer.IsVisible());
        harness.Click("Button_CloseSkills");
        Assert.False(harness.Layer.IsVisible());
        Assert.False(harness.InteractionState.IsInteractionBlocked());

        //  An existing exclusive blocker prevents opening.
        Assert.True(harness.InteractionState.TryBlockInteractionExclusive(out InteractionState.InteractionBlocker? blocker));
        using (blocker)
        {
            harness.PressKey(Key.K);
            Assert.False(harness.Layer.IsVisible());
        }
    }

    private static Skill CreateSkill(string id, string name, string category, string? icon)
    {
        return new Skill(
            id,
            name,
            category,
            icon,
            sources: [],
            levels: new SortedDictionary<int, int>
            {
                [1] = 10,
                [2] = 20,
            }
        );
    }

    private static ILocalization CreateSkillLocalization()
    {
        return new DictionaryLocalization(new Dictionary<string, string>
        {
            ["ui.panel.skill.list"] = "Skills",
            ["ui.label.skill.totalLevel"] = "Total level {0}",
            ["ui.label.skill.level"] = "Lvl {0}",
            ["ui.label.skill.totalXp"] = "Total {0} XP",
            ["ui.label.skill.xpProgress"] = "{0} / {1} XP",
            ["ui.label.skill.xpMax"] = "{0} XP (Max)",
            ["ui.label.skill.uncategorized"] = "Other",
            ["ui.message.skill.empty"] = "No skills available",
            ["skill.category.gathering"] = "Gathering",
            ["skill.category.misc"] = "Misc",
            ["skill.name.mining"] = "Mining",
            ["skill.name.building"] = "Building",
            ["skill.name.salvaging"] = "Salvaging",
        });
    }

    private sealed class PanelHarness : IDisposable
    {
        private readonly FakeInputService _input = new();
        private readonly Shader _uiShader = new("ui", []);
        private readonly SkillIconDatabase _iconDatabase;
        private readonly UIController _controller = new();
        private readonly UIBuilder<Material> _ui;

        public PanelHarness(
            ISkillDatabase skillDatabase,
            IAssetDatabase<Texture> textures,
            ActiveCharacterSave activeCharacterSave,
            ILocalization localization
        )
        {
            Shortcuts = new ShortcutService(_input, NullLogger.Instance);
            var motion = new ClientPlayerMotionProcessor(_input, new FakeWindowContext(), new FakePhysics(), new EventInvoker<PlayerMovedEvent>([]));
            InteractionState = new InteractionState(motion);
            IconLogger = new CapturingLogger<SkillIconDatabase>();
            _iconDatabase = new SkillIconDatabase(IconLogger, _uiShader, textures);
            _ui = new UIBuilder<Material>(1280, 720, new StubTextEngine(), _controller);

            Layer = new SkillsLayer(
                Shortcuts,
                InteractionState,
                skillDatabase,
                _iconDatabase,
                activeCharacterSave,
                localization
            );
        }

        public ShortcutService Shortcuts { get; }
        public InteractionState InteractionState { get; }
        public CapturingLogger<SkillIconDatabase> IconLogger { get; }
        public SkillsLayer Layer { get; }

        public void PressKey(Key key)
        {
            _input.HeldKeys.Add(key);
            _input.KeyPressed?.Invoke(_input, new KeyEventArgs(key));
            _input.HeldKeys.Remove(key);
            _input.KeyReleased?.Invoke(_input, new KeyEventArgs(key));
        }

        public List<RenderCommand<Material>> Render(double time = 0.1d)
        {
            Layer.RenderUI(time, _ui);
            return _ui.Build(time);
        }

        public ElementBounds GetElementBounds(string elementId) => _ui.GetElementBounds(elementId);

        public void Click(string elementId)
        {
            _controller.UpdateMouse(0, 0, UIController.MouseButtons.None);
            Render(0.1d);

            ElementBounds bounds = _ui.GetElementBounds(elementId);
            int x = bounds.Rect.Left + Math.Max(1, bounds.Rect.Size.X / 2);
            int y = bounds.Rect.Top + Math.Max(1, bounds.Rect.Size.Y / 2);

            _controller.UpdateMouse(x, y, UIController.MouseButtons.None);
            Render(0.2d);

            _controller.UpdateMouse(x, y, UIController.MouseButtons.Left);
            Render(0.3d);

            _controller.UpdateMouse(x, y, UIController.MouseButtons.None);
            Render(0.4d);
        }

        public void Dispose()
        {
            Layer.Close();
            _iconDatabase.Dispose();
            _uiShader.Dispose();
        }
    }

    private sealed class ListSkillDatabase(IReadOnlyList<Skill> skills) : ISkillDatabase
    {
        public IReadOnlyList<Skill> All => skills;

        public Result<Skill> Get(string id)
        {
            for (var i = 0; i < skills.Count; i++)
            {
                if (string.Equals(skills[i].ID, id, StringComparison.Ordinal))
                {
                    return Result<Skill>.FromSuccess(skills[i]);
                }
            }

            return Result<Skill>.FromFailure("Unknown skill");
        }

        public IReadOnlyList<Skill> Get(XPSource xpSource) => [];
    }

    private sealed class TestTextureDatabase(Dictionary<string, Texture> textures) : IAssetDatabase<Texture>
    {
        public Result<Texture> Get(string id)
        {
            return textures.TryGetValue(id, out Texture? texture)
                ? Result<Texture>.FromSuccess(texture)
                : Result<Texture>.FromFailure($"Unknown texture \"{id}\"");
        }
    }

    private sealed class DictionaryLocalization(Dictionary<string, string> entries) : ILocalization
    {
        public string? GetString(string key) => entries.TryGetValue(key, out string? value) ? value : key;

        public string? GetString(string key, string language) => GetString(key);

        public string? GetString(string key, CultureInfo culture) => GetString(key);
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter
        )
        {
            Entries.Add((logLevel, formatter(state, exception)));
        }
    }
}
