using System;
using System.Collections.Generic;
using Shoal.CommandLine;
using Swordfish.Library.IO;
using WaywardBeyond.Client.Shortcuts;

namespace WaywardBeyond.Client.UI.Settings;

/// <summary>Builds the keybindings settings category and detects global conflicts.</summary>
internal static class KeybindingsSettingsCategory
{
    public const string ROW_ID_PREFIX = "Settings.Keybind.";

    private sealed class Bucket(string sectionKey)
    {
        public readonly string SectionKey = sectionKey;
        public readonly List<ShortcutInfo> Shortcuts = [];
    }

    /// <summary>Returns whether developer keybindings are visible in the current build or launch configuration.</summary>
    public static bool IsDeveloperModeEnabled(CommandLineArgs? commandLineArgs)
    {
#if DEBUG
        return true;
#else
        return IsDebugFlagPresent(commandLineArgs);
#endif
    }

    /// <summary>Returns whether the process arguments enable debug mode via <c>-debug</c> or <c>--debug</c>.</summary>
    public static bool IsDebugFlagPresent(CommandLineArgs? commandLineArgs)
    {
        return commandLineArgs != null && commandLineArgs.GetFlag("debug");
    }

    public static SettingsCategory Build(
        SettingsDraft draft,
        IShortcutService shortcutService,
        Action reset,
        bool includeDeveloper = true
    )
    {
        var movement = new Bucket("ui.settings.section.movement");
        var interaction = new Bucket("ui.settings.section.interaction");
        var inventory = new Bucket("ui.settings.section.inventory");
        var general = new Bucket("ui.settings.section.general");
        var developer = new Bucket("ui.settings.section.developer");
        var customBuckets = new List<Bucket>();

        IReadOnlyList<ShortcutInfo> registered = shortcutService.GetShortcuts();
        for (var i = 0; i < registered.Count; i++)
        {
            ShortcutInfo info = registered[i];
            if (!info.Rebindable || (!includeDeveloper && IsDeveloperShortcut(in info)))
            {
                continue;
            }

            if (!draft.Keybindings.ContainsKey(info.Id))
            {
                draft.Keybindings[info.Id] = info.CurrentBinding;
            }

            Bucket target = ResolveBucket(in info, movement, interaction, inventory, general, developer, customBuckets);
            target.Shortcuts.Add(info);
        }

        var rows = new List<SettingsRow>();
        AppendBucket(rows, draft, movement);
        AppendBucket(rows, draft, interaction);
        AppendBucket(rows, draft, inventory);
        AppendBucket(rows, draft, general);
        AppendBucket(rows, draft, developer);
        for (var i = 0; i < customBuckets.Count; i++)
        {
            AppendBucket(rows, draft, customBuckets[i]);
        }

        rows.Add(new SettingsResetRow("Settings.Reset.keybindings", reset));

        return new SettingsCategory(
            id: "keybindings",
            titleKey: "ui.settings.category.keybindings",
            rows: rows.ToArray()
        );
    }

    /// <summary>Collects distinct bound triggers reserved by non-rebindable shortcuts.</summary>
    public static ShortcutBinding[] CollectFixedBindings(IShortcutService shortcutService)
    {
        IReadOnlyList<ShortcutInfo> registered = shortcutService.GetShortcuts();
        var fixedBindings = new List<ShortcutBinding>();
        for (var i = 0; i < registered.Count; i++)
        {
            ShortcutInfo info = registered[i];
            if (info.Rebindable || !info.CurrentBinding.IsBound || fixedBindings.Contains(info.CurrentBinding))
            {
                continue;
            }

            fixedBindings.Add(info.CurrentBinding);
        }

        return fixedBindings.ToArray();
    }

    /// <summary>Evaluates conflicts across all bound keybinding entries in <paramref name="category"/>.</summary>
    public static bool UpdateConflicts(
        SettingsCategory category,
        IReadOnlyList<ShortcutBinding>? fixedBindings = null
    )
    {
        SettingsEntry[] entries = category.Entries;
        var hasConflicts = false;

        for (var i = 0; i < entries.Length; i++)
        {
            if (entries[i] is not KeybindingEntry entry)
            {
                continue;
            }

            entry.HasConflict = false;
            if (entry.IsUnbound)
            {
                continue;
            }

            if (fixedBindings != null && MatchesAny(entry.Value, fixedBindings))
            {
                entry.HasConflict = true;
                hasConflicts = true;
            }
        }

        for (var i = 0; i < entries.Length; i++)
        {
            if (entries[i] is not KeybindingEntry first || !first.Value.IsBound)
            {
                continue;
            }

            for (int j = i + 1; j < entries.Length; j++)
            {
                if (entries[j] is not KeybindingEntry second || !second.Value.IsBound)
                {
                    continue;
                }

                if (!ScopesOverlap(first.Scope, second.Scope))
                {
                    continue;
                }

                if (first.Value != second.Value)
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(first.SharedBindingGroup)
                    && string.Equals(first.SharedBindingGroup, second.SharedBindingGroup, StringComparison.Ordinal))
                {
                    continue;
                }

                first.HasConflict = true;
                second.HasConflict = true;
                hasConflicts = true;
            }
        }

        return hasConflicts;
    }

    private static bool ScopesOverlap(KeybindingScope left, KeybindingScope right)
    {
        if (left == KeybindingScope.Global || right == KeybindingScope.Global || left == right)
        {
            return true;
        }

        return (left == KeybindingScope.World && IsWorldSubScope(right))
            || (right == KeybindingScope.World && IsWorldSubScope(left));
    }

    private static bool IsWorldSubScope(KeybindingScope scope)
    {
        return scope is KeybindingScope.BrickPlacement
            or KeybindingScope.PaintSprayer
            or KeybindingScope.Drill
            or KeybindingScope.RegionTool;
    }

    private static KeybindingScope ResolveScope(in ShortcutInfo info)
    {
        if (string.Equals(info.Id, GameShortcuts.CHANGE_SHAPE, StringComparison.Ordinal)
            || string.Equals(info.Id, GameShortcuts.CHANGE_ORIENTATION, StringComparison.Ordinal))
        {
            return KeybindingScope.BrickPlacement;
        }

        if (string.Equals(info.Id, GameShortcuts.SELECT_PAINT_COLOR, StringComparison.Ordinal))
        {
            return KeybindingScope.PaintSprayer;
        }

        if (string.Equals(info.Id, GameShortcuts.SELECT_PAINT_TEXTURE, StringComparison.Ordinal))
        {
            return KeybindingScope.Drill;
        }

        if (string.Equals(info.Id, GameShortcuts.BLUEPRINT_LIBRARY, StringComparison.Ordinal))
        {
            return KeybindingScope.World;
        }

        if (string.Equals(info.Id, GameShortcuts.REGION_TOOL_ACTIONS, StringComparison.Ordinal)
            || string.Equals(info.Category, "RegionTool", StringComparison.Ordinal))
        {
            return KeybindingScope.RegionTool;
        }

        if (string.Equals(info.Category, "Movement", StringComparison.Ordinal)
            || string.Equals(info.Category, "Interaction", StringComparison.Ordinal)
            || string.Equals(info.Id, GameShortcuts.OPEN_CHAT, StringComparison.Ordinal))
        {
            return KeybindingScope.World;
        }

        if (string.Equals(info.Id, GameShortcuts.INVENTORY_MOVE_STACK, StringComparison.Ordinal)
            || string.Equals(info.Id, GameShortcuts.INVENTORY_SPLIT_STACK, StringComparison.Ordinal)
            || string.Equals(info.Id, GameShortcuts.INVENTORY_QUICK_MOVE, StringComparison.Ordinal))
        {
            return KeybindingScope.Inventory;
        }

        if (string.Equals(info.Id, GameShortcuts.SEND_CHAT, StringComparison.Ordinal))
        {
            return KeybindingScope.Chat;
        }

        return KeybindingScope.Global;
    }

    private static bool MatchesAny(ShortcutBinding target, IReadOnlyList<ShortcutBinding> bindings)
    {
        for (var i = 0; i < bindings.Count; i++)
        {
            if (bindings[i] == target)
            {
                return true;
            }
        }

        return false;
    }

    private static Bucket ResolveBucket(
        in ShortcutInfo info,
        Bucket movement,
        Bucket interaction,
        Bucket inventory,
        Bucket general,
        Bucket developer,
        List<Bucket> customBuckets
    )
    {
        if (string.Equals(info.Category, "Movement", StringComparison.Ordinal))
        {
            return movement;
        }

        if (string.Equals(info.Category, "Interaction", StringComparison.Ordinal))
        {
            return interaction;
        }

        if (string.Equals(info.Id, GameShortcuts.INVENTORY, StringComparison.Ordinal)
            || string.Equals(info.Category, "Inventory", StringComparison.Ordinal))
        {
            return inventory;
        }

        if (IsDeveloperShortcut(in info))
        {
            return developer;
        }

        if (string.IsNullOrEmpty(info.Category)
            || string.Equals(info.Category, "Chat", StringComparison.Ordinal)
            || string.Equals(info.Category, "General", StringComparison.Ordinal)
            || string.Equals(info.Category, "UI", StringComparison.Ordinal))
        {
            return general;
        }

        for (var i = 0; i < customBuckets.Count; i++)
        {
            if (string.Equals(customBuckets[i].SectionKey, info.Category, StringComparison.Ordinal))
            {
                return customBuckets[i];
            }
        }

        var custom = new Bucket(info.Category);
        customBuckets.Add(custom);
        return custom;
    }

    private static bool IsDeveloperShortcut(in ShortcutInfo info)
    {
        return string.Equals(info.Category, "Developer", StringComparison.Ordinal)
            || (info.Category != null && info.Category.StartsWith("Developer/", StringComparison.Ordinal));
    }

    private static void AppendBucket(List<SettingsRow> rows, SettingsDraft draft, Bucket bucket)
    {
        if (bucket.Shortcuts.Count == 0)
        {
            return;
        }

        bucket.Shortcuts.Sort(CompareShortcutOrder);
        rows.Add(new SettingsSection(bucket.SectionKey));

        for (var i = 0; i < bucket.Shortcuts.Count; i++)
        {
            ShortcutInfo info = bucket.Shortcuts[i];
            string shortcutId = info.Id;
            rows.Add(new KeybindingEntry(
                id: ROW_ID_PREFIX + shortcutId,
                labelKey: ResolveLabelKey(in info),
                shortcutId: shortcutId,
                sharedBindingGroup: info.SharedBindingGroup,
                scope: ResolveScope(in info),
                get: () => draft.Keybindings[shortcutId],
                set: value => draft.Keybindings[shortcutId] = value,
                defaultBinding: info.DefaultBinding
            ));
        }
    }

    private static int CompareShortcutOrder(ShortcutInfo left, ShortcutInfo right)
    {
        int leftRank = GetSortRank(left.Id);
        int rightRank = GetSortRank(right.Id);
        if (leftRank != rightRank)
        {
            return leftRank.CompareTo(rightRank);
        }

        return string.Compare(left.Id, right.Id, StringComparison.Ordinal);
    }

    private static int GetSortRank(string id)
    {
        return id switch
        {
            GameShortcuts.MOVE_FORWARD => 0,
            GameShortcuts.MOVE_BACKWARD => 1,
            GameShortcuts.STRAFE_LEFT => 2,
            GameShortcuts.STRAFE_RIGHT => 3,
            GameShortcuts.MOVE_UP => 4,
            GameShortcuts.MOVE_DOWN => 5,
            GameShortcuts.ROLL_LEFT => 6,
            GameShortcuts.ROLL_RIGHT => 7,

            GameShortcuts.PRIMARY_ACTION => 100,
            GameShortcuts.SECONDARY_ACTION => 101,
            GameShortcuts.PICK_BRICK => 102,
            GameShortcuts.SNAP_PLACEMENT => 103,
            GameShortcuts.CHANGE_SHAPE => 104,
            GameShortcuts.CHANGE_ORIENTATION => 105,
            GameShortcuts.SELECT_PAINT_COLOR => 106,
            GameShortcuts.SELECT_PAINT_TEXTURE => 107,
            GameShortcuts.REGION_TOOL_ACTIONS => 108,

            GameShortcuts.INVENTORY => 200,
            GameShortcuts.INVENTORY_MOVE_STACK => 201,
            GameShortcuts.INVENTORY_SPLIT_STACK => 202,
            GameShortcuts.INVENTORY_QUICK_MOVE => 203,
            "Inventory/Slot 1" => 210,
            "Inventory/Slot 2" => 211,
            "Inventory/Slot 3" => 212,
            "Inventory/Slot 4" => 213,
            "Inventory/Slot 5" => 214,
            "Inventory/Slot 6" => 215,
            "Inventory/Slot 7" => 216,
            "Inventory/Slot 8" => 217,
            "Inventory/Slot 9" => 218,

            GameShortcuts.BACK => 300,
            GameShortcuts.SKILLS => 301,
            GameShortcuts.OPEN_CHAT => 302,
            GameShortcuts.SEND_CHAT => 303,
            GameShortcuts.QUICKSAVE => 304,
            GameShortcuts.TAKE_SCREENSHOT => 305,
            GameShortcuts.TOGGLE_UI => 306,
            GameShortcuts.FEEDBACK_FORM => 307,
            GameShortcuts.TOGGLE_FULLSCREEN => 308,
            GameShortcuts.ALT_TOGGLE_FULLSCREEN => 309,

            GameShortcuts.TOGGLE_DEBUG => 400,
            GameShortcuts.TOGGLE_VOXEL_GIZMOS => 401,
            GameShortcuts.TOGGLE_UI_DEBUG => 402,
            GameShortcuts.CYCLE_SAO_DEBUG => 403,
            GameShortcuts.TOGGLE_PHYSICS_DEBUG => 404,

            _ => int.MaxValue,
        };
    }

    private static string ResolveLabelKey(in ShortcutInfo info)
    {
        return info.Id switch
        {
            GameShortcuts.MOVE_FORWARD => "ui.settings.keybind.moveForward",
            GameShortcuts.MOVE_BACKWARD => "ui.settings.keybind.moveBackward",
            GameShortcuts.STRAFE_LEFT => "ui.settings.keybind.strafeLeft",
            GameShortcuts.STRAFE_RIGHT => "ui.settings.keybind.strafeRight",
            GameShortcuts.MOVE_UP => "ui.settings.keybind.moveUp",
            GameShortcuts.MOVE_DOWN => "ui.settings.keybind.moveDown",
            GameShortcuts.ROLL_LEFT => "ui.settings.keybind.rollLeft",
            GameShortcuts.ROLL_RIGHT => "ui.settings.keybind.rollRight",

            GameShortcuts.PRIMARY_ACTION => "ui.settings.keybind.primaryAction",
            GameShortcuts.SECONDARY_ACTION => "ui.settings.keybind.secondaryAction",
            GameShortcuts.PICK_BRICK => "ui.settings.keybind.pickBrick",
            GameShortcuts.SNAP_PLACEMENT => "ui.settings.keybind.snapPlacement",
            GameShortcuts.CHANGE_SHAPE => "ui.settings.keybind.changeShape",
            GameShortcuts.CHANGE_ORIENTATION => "ui.settings.keybind.changeOrientation",
            GameShortcuts.SELECT_PAINT_COLOR => "ui.settings.keybind.selectPaintColor",
            GameShortcuts.SELECT_PAINT_TEXTURE => "ui.settings.keybind.selectPaintTexture",

            GameShortcuts.INVENTORY => "ui.settings.keybind.inventory",
            GameShortcuts.INVENTORY_MOVE_STACK => "ui.settings.keybind.inventoryMove",
            GameShortcuts.INVENTORY_SPLIT_STACK => "ui.settings.keybind.inventorySplit",
            GameShortcuts.INVENTORY_QUICK_MOVE => "ui.settings.keybind.inventoryQuickMove",
            "Inventory/Slot 1" => "ui.settings.keybind.slot1",
            "Inventory/Slot 2" => "ui.settings.keybind.slot2",
            "Inventory/Slot 3" => "ui.settings.keybind.slot3",
            "Inventory/Slot 4" => "ui.settings.keybind.slot4",
            "Inventory/Slot 5" => "ui.settings.keybind.slot5",
            "Inventory/Slot 6" => "ui.settings.keybind.slot6",
            "Inventory/Slot 7" => "ui.settings.keybind.slot7",
            "Inventory/Slot 8" => "ui.settings.keybind.slot8",
            "Inventory/Slot 9" => "ui.settings.keybind.slot9",

            GameShortcuts.BACK => "ui.settings.keybind.back",
            GameShortcuts.SKILLS => "ui.settings.keybind.skills",
            GameShortcuts.OPEN_CHAT => "ui.settings.keybind.openChat",
            GameShortcuts.SEND_CHAT => "ui.settings.keybind.sendChat",
            GameShortcuts.QUICKSAVE => "ui.settings.keybind.quicksave",
            GameShortcuts.TAKE_SCREENSHOT => "ui.settings.keybind.screenshot",
            GameShortcuts.TOGGLE_UI => "ui.settings.keybind.toggleUi",
            GameShortcuts.FEEDBACK_FORM => "ui.settings.keybind.feedback",
            GameShortcuts.TOGGLE_FULLSCREEN => "ui.settings.keybind.toggleFullscreen",
            GameShortcuts.ALT_TOGGLE_FULLSCREEN => "ui.settings.keybind.altToggleFullscreen",

            GameShortcuts.TOGGLE_DEBUG => "ui.settings.keybind.toggleDebug",
            GameShortcuts.TOGGLE_VOXEL_GIZMOS => "ui.settings.keybind.toggleVoxelGizmos",
            GameShortcuts.TOGGLE_UI_DEBUG => "ui.settings.keybind.toggleUiDebug",
            GameShortcuts.CYCLE_SAO_DEBUG => "ui.settings.keybind.cycleSaoDebug",
            GameShortcuts.TOGGLE_PHYSICS_DEBUG => "ui.settings.keybind.togglePhysicsDebug",

            _ => info.Name,
        };
    }
}
