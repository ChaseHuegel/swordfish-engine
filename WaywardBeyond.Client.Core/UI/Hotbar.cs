using ImGuiNET;
using Swordfish.Graphics;
using Swordfish.Library.Constraints;
using Swordfish.Types;
using Swordfish.UI;
using Swordfish.UI.Elements;

namespace WaywardBeyond.Client.Core.UI;

public class Hotbar : CanvasElement
{
    private TextElement[] _names;
    private TextElement[] _counts;
    
    public Hotbar(IWindowContext windowContext, IUIContext uiContext) : base(uiContext, windowContext, "Hotbar")
    {
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

            var label = new TextElement(slotNumber.ToString())
            {
                Wrap = false,
                Constraints = new RectConstraints
                {
                    Anchor = ConstraintAnchor.TOP_LEFT
                },
            };
            slot.Content.Add(label);
            
            var name = new TextElement(string.Empty)
            {
                Wrap = false,
                Constraints = new RectConstraints
                {
                    Anchor = ConstraintAnchor.CENTER_LEFT,
                    Y = new RelativeConstraint(-0.2f),
                },
            };
            slot.Content.Add(name);
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
            slot.Content.Add(count);
            _counts[i] = count;
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