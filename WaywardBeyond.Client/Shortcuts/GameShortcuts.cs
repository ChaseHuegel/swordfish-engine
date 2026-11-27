using System;
using Swordfish.Library.IO;

namespace WaywardBeyond.Client.Shortcuts;

/// <summary>Defines canonical game shortcut identifiers, shared groups, and definition factories.</summary>
internal static class GameShortcuts
{
    public const string TOOL_RADIAL_GROUP = "ToolRadial";

    public const string MOVE_FORWARD = "Movement/Move forward";
    public const string MOVE_BACKWARD = "Movement/Move backward";
    public const string STRAFE_LEFT = "Movement/Strafe left";
    public const string STRAFE_RIGHT = "Movement/Strafe right";
    public const string MOVE_UP = "Movement/Move up";
    public const string MOVE_DOWN = "Movement/Move down";
    public const string ROLL_LEFT = "Movement/Roll left";
    public const string ROLL_RIGHT = "Movement/Roll right";

    public const string PRIMARY_ACTION = "Interaction/Primary action";
    public const string SECONDARY_ACTION = "Interaction/Secondary action";
    public const string PICK_BRICK = "Interaction/Pick brick";
    public const string SNAP_PLACEMENT = "Interaction/Snap Placement";
    public const string CHANGE_SHAPE = "Interaction/Change shape";
    public const string CHANGE_ORIENTATION = "Interaction/Change orientation";
    public const string SELECT_PAINT_COLOR = "Interaction/Select paint color";
    public const string SELECT_PAINT_TEXTURE = "Interaction/Select paint texture";
    public const string BLUEPRINT_TOOL_ACTIONS = "Interaction/Blueprint tool actions";

    public const string BLUEPRINT_LIBRARY = "Blueprints/Open library";
    public const string BLUEPRINT_COPY = "Blueprints/Copy";
    public const string BLUEPRINT_CUT = "Blueprints/Cut";
    public const string BLUEPRINT_PASTE_MODE = "Blueprints/Paste mode";
    public const string BLUEPRINT_ROTATE_YAW = "Blueprints/Rotate yaw";
    public const string BLUEPRINT_ROTATE_PITCH = "Blueprints/Rotate pitch";
    public const string BLUEPRINT_MIRROR_HORIZONTAL = "Blueprints/Mirror horizontal";
    public const string BLUEPRINT_MIRROR_VERTICAL = "Blueprints/Mirror vertical";
    public const string BLUEPRINT_OVERWRITE_EMPTY = "Blueprints/Overwrite empty";

    public const string INVENTORY = "UI/Inventory";
    public const string INVENTORY_MOVE_STACK = "Inventory/Move stack";
    public const string INVENTORY_SPLIT_STACK = "Inventory/Split stack";
    public const string INVENTORY_QUICK_MOVE = "Inventory/Quick move";

    public const string BACK = "General/Back";
    public const string OPEN_CHAT = "Chat/Open Chat";
    public const string SEND_CHAT = "Chat/Send Chat";
    public const string QUICKSAVE = "General/Quicksave";
    public const string TAKE_SCREENSHOT = "General/Take screenshot";
    public const string TOGGLE_UI = "UI/Toggle UI";
    public const string FEEDBACK_FORM = "UI/Feedback form";
    public const string TOGGLE_FULLSCREEN = "UI/Toggle Fullscreen";
    public const string ALT_TOGGLE_FULLSCREEN = "UI/Alt Toggle Fullscreen";

    public const string TOGGLE_DEBUG = "Developer/Toggle debug";
    public const string TOGGLE_VOXEL_GIZMOS = "Developer/Debug/Toggle voxel gizmos";
    public const string TOGGLE_UI_DEBUG = "Developer/Debug/Toggle UI debug";
    public const string CYCLE_SAO_DEBUG = "Developer/Debug/Cycle SAO debug";
    public const string TOGGLE_PHYSICS_DEBUG = "Developer/Debug/Toggle physics debug";

    /// <summary>Returns the canonical shortcut identifier for a hotbar and inventory slot (0-based).</summary>
    public static string Slot(int index)
    {
        return $"Inventory/Slot {index + 1}";
    }

    /// <summary>Creates a held movement shortcut definition.</summary>
    public static Shortcut CreateHeld(string category, string name, Key key)
    {
        return new Shortcut
        {
            Category = category,
            Name = name,
            Key = key,
            IgnoreUnboundModifiers = true,
            IsEnabled = Shortcut.DefaultEnabled,
        };
    }

    /// <summary>Creates the primary interaction shortcut definition.</summary>
    public static Shortcut CreatePrimaryAction()
    {
        return new Shortcut
        {
            Category = "Interaction",
            Name = "Primary action",
            MouseButton = MouseButton.Left,
            IgnoreUnboundModifiers = true,
            IsEnabled = Shortcut.DefaultEnabled,
        };
    }

    /// <summary>Creates the secondary interaction shortcut definition.</summary>
    public static Shortcut CreateSecondaryAction()
    {
        return new Shortcut
        {
            Category = "Interaction",
            Name = "Secondary action",
            MouseButton = MouseButton.Right,
            IgnoreUnboundModifiers = true,
            IsEnabled = Shortcut.DefaultEnabled,
        };
    }

    /// <summary>Creates the pick-brick shortcut definition.</summary>
    public static Shortcut CreatePickBrick()
    {
        return new Shortcut
        {
            Category = "Interaction",
            Name = "Pick brick",
            MouseButton = MouseButton.Middle,
            IgnoreUnboundModifiers = true,
            IsEnabled = Shortcut.DefaultEnabled,
        };
    }

    /// <summary>Creates a shared slot shortcut definition for hotbar and inventory selection.</summary>
    public static Shortcut CreateSlot(int index)
    {
        return new Shortcut
        {
            Category = "Inventory",
            Name = $"Slot {index + 1}",
            Modifiers = ShortcutModifiers.None,
            Key = Key.D1 + index,
            IsEnabled = Shortcut.DefaultEnabled,
        };
    }

    /// <summary>Creates the inventory stack move and pickup shortcut definition.</summary>
    public static Shortcut CreateInventoryMoveStack()
    {
        return new Shortcut
        {
            Category = "Inventory",
            Name = "Move stack",
            MouseButton = MouseButton.Left,
            IgnoreUnboundModifiers = true,
            IsEnabled = Shortcut.DefaultEnabled,
        };
    }

    /// <summary>Creates the inventory stack split and distribution shortcut definition.</summary>
    public static Shortcut CreateInventorySplitStack()
    {
        return new Shortcut
        {
            Category = "Inventory",
            Name = "Split stack",
            MouseButton = MouseButton.Right,
            IgnoreUnboundModifiers = true,
            IsEnabled = Shortcut.DefaultEnabled,
        };
    }

    /// <summary>Creates the inventory quick-move shortcut definition.</summary>
    public static Shortcut CreateInventoryQuickMove()
    {
        return new Shortcut
        {
            Category = "Inventory",
            Name = "Quick move",
            Modifiers = ShortcutModifiers.Shift,
            MouseButton = MouseButton.Left,
            IgnoreUnboundModifiers = false,
            IsEnabled = Shortcut.DefaultEnabled,
        };
    }

    /// <summary>Creates the shared back, close, cancel, and pause shortcut definition.</summary>
    public static Shortcut CreateBack()
    {
        return new Shortcut
        {
            Category = "General",
            Name = "Back",
            Modifiers = ShortcutModifiers.None,
            Key = Key.Esc,
            IsEnabled = Shortcut.DefaultEnabled,
        };
    }

    /// <summary>Registers a shortcut or attaches an additional handler when already registered.</summary>
    public static void RegisterOrAddHandler(
        IShortcutService shortcutService,
        Shortcut definition,
        Func<bool>? isEnabled = null,
        Action? action = null,
        Action? released = null
    )
    {
        if (action != null || released != null)
        {
            definition.IsEnabled = isEnabled ?? Shortcut.DefaultEnabled;
            definition.Action = action!;
            definition.Released = released!;
        }

        if (!shortcutService.RegisterShortcut(definition) && action != null)
        {
            shortcutService.AddHandler(definition.GetId(), isEnabled ?? Shortcut.DefaultEnabled, action, released);
        }
    }

    /// <summary>Populates <paramref name="labels"/> from the live slot shortcut bindings, falling back to 1-9.</summary>
    public static void ResolveSlotLabels(IShortcutService shortcutService, string[] labels)
    {
        for (var i = 0; i < labels.Length; i++)
        {
            Swordfish.Library.Util.Result<ShortcutBinding> result = shortcutService.GetBinding(Slot(i));
            labels[i] = result.Success
                ? (result.Value.IsBound ? result.Value.ToDisplayString() : string.Empty)
                : (i + 1).ToString();
        }
    }
}
