using System.Numerics;
using ImGuiNET;
using Swordfish.Types;

namespace Swordfish.UI.ImGuiNET;

//  Based on BeginGroupPanel by thedmd
//      https://github.com/ocornut/imgui/issues/1496#issuecomment-655048353
public partial class ImGuiEx
{
    private static readonly Stack<Pane> PaneStack = new();

    private struct Pane
    {
        public Rect2 LabelRect;
        public bool Border;
        public bool Title;

        public Pane(Rect2 labelRect, bool border, bool title)
        {
            LabelRect = labelRect;
            Border = border;
            Title = title;
        }
    }

    /// <summary>
    ///     A pane for grouping element that can resize to fit it's content.
    /// </summary>
    public static void BeginPane() => BeginPane(null, Vector2.Zero, false);

    /// <inheritdoc cref="BeginPane()"/>
    public static void BeginPane(bool border) => BeginPane(null, Vector2.Zero, border);

    /// <inheritdoc cref="BeginPane()"/>
    public static void BeginPane(string? name) => BeginPane(name, Vector2.Zero, true);

    /// <inheritdoc cref="BeginPane()"/>
    public static void BeginPane(string? name, bool border) => BeginPane(name, Vector2.Zero, border);

    /// <inheritdoc cref="BeginPane(string?)"/>
    public static void BeginPane(string? name, Vector2 size) => BeginPane(name, size, true);

    /// <inheritdoc cref="BeginPane(string?)"/>
    public static void BeginPane(string? name, Vector2 size, bool border)
    {
        var title = !string.IsNullOrWhiteSpace(name);

        ImGui.BeginGroup();

        var itemSpacing = ImGui.GetStyle().ItemSpacing;

        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, Vector2.Zero);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, Vector2.Zero);

        var frameHeight = ImGui.GetFrameHeight();

        ImGui.BeginGroup();

        var effectiveSize = size;
        if (size.X < 0f)
            effectiveSize.X = ImGui.GetContentRegionAvail().X - frameHeight;

        ImGui.Dummy(new Vector2(effectiveSize.X, 0f));

        var frameHeightHalf = new Vector2(frameHeight * 0.5f, 0f);

        if (title) ImGui.Dummy(frameHeightHalf);

        if (title) ImGui.SameLine(0f, 0f);
        ImGui.BeginGroup();

        if (title) ImGui.Dummy(frameHeightHalf);

        Vector2 labelMin, labelMax;
        if (title)
        {
            ImGui.SameLine(0f, 0f);
            ImGui.TextUnformatted(name);

            labelMin = ImGui.GetItemRectMin();
            labelMax = ImGui.GetItemRectMax();

            ImGui.SameLine(0f, 0f);
            ImGui.Dummy(new Vector2(0f, frameHeight + itemSpacing.Y));
        }
        else
        {
            labelMin = Vector2.Zero;
            labelMax = Vector2.Zero;
        }

        ImGui.BeginGroup();

        ImGui.PopStyleVar(2);

        ImGui.PushItemWidth(Math.Max(0f, ImGui.CalcItemWidth() - frameHeight));

        PaneStack.Push(
            new Pane
            (
                new Rect2(labelMin, labelMax),
                border,
                title
            )
        );
    }

    public static void EndPane()
    {
        var pane = PaneStack.Pop();

        ImGui.PopItemWidth();

        var itemSpacing = ImGui.GetStyle().ItemSpacing;

        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, Vector2.Zero);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, Vector2.Zero);

        var frameHeight = ImGui.GetFrameHeight();

        ImGui.EndGroup();
        ImGui.EndGroup();

        ImGui.SameLine(0, 0);
        if (pane.Title) ImGui.Dummy(new Vector2(frameHeight * 0.5f, 0f));

        if (pane.Title) ImGui.Dummy(new Vector2(0f, frameHeight * 0.5f - itemSpacing.Y));

        ImGui.EndGroup();

        var itemMin = ImGui.GetItemRectMin();
        var itemMax = ImGui.GetItemRectMax();

        var labelRect = pane.LabelRect;
        labelRect.Min.X -= itemSpacing.X;
        labelRect.Max.X += itemSpacing.X;

        if (pane.Border)
        {
            var halfFrame = pane.Title ? new Vector2(frameHeight * 0.25f, frameHeight) * 0.5f : Vector2.Zero;
            var frameRect = new Rect2(itemMin + halfFrame, itemMax - new Vector2(halfFrame.X, -halfFrame.Y));
            for (int i = 0; i < 4; i++)
            {
                switch (i)
                {
                    case 0:
                        ImGui.PushClipRect(new Vector2(-float.MaxValue), new Vector2(labelRect.Min.X, float.MaxValue), true);
                        break;
                    case 1:
                        ImGui.PushClipRect(new Vector2(labelRect.Max.X, -float.MaxValue), new Vector2(float.MaxValue), true);
                        break;
                    case 2:
                        ImGui.PushClipRect(new Vector2(labelRect.Min.X, -float.MaxValue), new Vector2(labelRect.Max.X, labelRect.Min.Y), true);
                        break;
                    case 3:
                        ImGui.PushClipRect(new Vector2(labelRect.Min.X, labelRect.Max.Y), new Vector2(labelRect.Max.X, float.MaxValue), true);
                        break;
                }

                var drawList = ImGui.GetWindowDrawList();
                drawList.AddRect(frameRect.Min, frameRect.Max, ImGui.GetColorU32(ImGuiCol.Border), halfFrame.X);

                ImGui.PopClipRect();
            }
        }

        ImGui.PopStyleVar(2);

        if (pane.Title)
            ImGui.Dummy(new Vector2(0f, frameHeight * 0.5f + itemSpacing.Y));

        ImGui.EndGroup();
    }
}
