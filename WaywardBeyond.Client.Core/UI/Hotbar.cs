using ImGuiNET;
using Swordfish.Graphics;
using Swordfish.Library.Constraints;
using Swordfish.Library.IO;
using Swordfish.Library.Types;
using Swordfish.Types;
using Swordfish.UI;
using Swordfish.UI.Elements;
using Color = System.Drawing.Color;

namespace WaywardBeyond.Client.Core.UI;

public class Hotbar : CanvasElement
{
    private TextElement[] _names;
    private TextElement[] _counts;
    private ColorBlockElement[] _colorBlocks;
    
    public readonly DataBinding<int> ActiveSlot = new(-1);
    
    public Hotbar(IWindowContext windowContext, IUIContext uiContext, IShortcutService shortcutService) : base(uiContext, windowContext, "Hotbar")
    {
        ActiveSlot.Changed += OnActiveSlotChanged;
        
        Flags = ImGuiWindowFlags.NoResize |
                ImGuiWindowFlags.NoCollapse |
                ImGuiWindowFlags.NoTitleBar |
                ImGuiWindowFlags.AlwaysAutoResize |
                ImGuiWindowFlags.NoBringToFrontOnFocus |
                ImGuiWindowFlags.NoScrollbar |
                ImGuiWindowFlags.NoMove;
        
        Constraints = new RectConstraints
        {
            Anchor = ConstraintAnchor.BOTTOM_CENTER,
            X = new AbsoluteConstraint(0f),
            Y = new RelativeConstraint(0.01f),
            Width = new RelativeConstraint(0.285f),
            Height = new RelativeConstraint(0.07f)
        };

        var layoutGroup = new LayoutGroup
        {
            Layout = ElementAlignment.HORIZONTAL,
            Flags = ImGuiWindowFlags.NoScrollbar,
            Constraints = new RectConstraints
            {
                Anchor = ConstraintAnchor.CENTER,
                Width = new FillConstraint(),
                Height = new FillConstraint()
            }
        };
        Content.Add(layoutGroup);

        const int slotCount = 9;
        _names = new TextElement[slotCount];
        _counts = new TextElement[slotCount];
        _colorBlocks = new ColorBlockElement[slotCount];
        for (int i = 0; i < slotCount; i++)
        {
            int slotNumber = i + 1;

            var slot = new PanelElement(slotNumber.ToString())
            {
                Border = true,
                TitleBar = false,
                Flags = ImGuiWindowFlags.NoScrollbar,
                Constraints = new RectConstraints
                {
                    Width = new RelativeConstraint(0.90f / slotCount),
                    Height = new AspectConstraint(1f)
                }
            };
            layoutGroup.Content.Add(slot);
            
            var colorBlock = new ColorBlockElement(Color.White);
            slot.Content.Add(colorBlock);
            _colorBlocks[i] = colorBlock;

            var label = new TextElement(slotNumber.ToString())
            {
                Wrap = false,
                Constraints = new RectConstraints
                {
                    Anchor = ConstraintAnchor.TOP_LEFT
                },
            };
            colorBlock.Content.Add(label);
            
            var name = new TextElement(string.Empty)
            {
                Wrap = false,
                Constraints = new RectConstraints
                {
                    Anchor = ConstraintAnchor.CENTER_LEFT,
                    Y = new RelativeConstraint(-0.2f),
                },
            };
            colorBlock.Content.Add(name);
            _names[i] = name;

            var count = new TextElement(string.Empty)
            {
                Wrap = false,
                Constraints = new RectConstraints
                {
                    Anchor = ConstraintAnchor.CENTER_RIGHT,
                    Y = new RelativeConstraint(0.2f),
                },
            };
            colorBlock.Content.Add(count);
            _counts[i] = count;

            int slotIndex = i;
            var shortcut = new Shortcut
            {
                Name = $"Slot {slotNumber}",
                Category = "Interaction",
                Modifiers = ShortcutModifiers.None,
                Key = Key.D1 + slotIndex,
                IsEnabled = Shortcut.DefaultEnabled,
                Action = () => ActiveSlot.Set(slotIndex),
            };
            shortcutService.RegisterShortcut(shortcut);
        }
        
        ActiveSlot.Set(0);
    }

    private void OnActiveSlotChanged(object? sender, DataChangedEventArgs<int> e)
    {
        if (e.OldValue >= 0 && e.OldValue < _colorBlocks.Length)
        {
            _colorBlocks[e.OldValue].Color = Color.White;
        }

        if (e.NewValue >= 0 && e.NewValue < _colorBlocks.Length)
        {
            _colorBlocks[e.NewValue].Color = Color.Green;
        }
    }

    public void UpdateSlot(int slot, string name, int count)
    {
        _names[slot].Text = name;
        _counts[slot].Text = count.ToString();
    }
    
    public void UpdateSlot(int slot, int count)
    {
        _counts[slot].Text = count != 0 ? count.ToString() : string.Empty;
    }
    
    public void Clear(int slot)
    {
        _names[slot].Text = string.Empty;
        _counts[slot].Text = string.Empty;
    }
}