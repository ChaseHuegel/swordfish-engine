using System.Numerics;
using ImGuiNET;
using Swordfish.Types;
// ReSharper disable UnusedMember.Global

namespace Swordfish.UI.ImGuiNET;

//  Based on BeginGroupPanel by thedmd
//      https://github.com/ocornut/imgui/issues/1496#issuecomment-655048353
// ReSharper disable once PartialTypeWithSinglePart
public static partial class ImGuiEx
{
    private static readonly Stack<Pane> _paneStack = new();

    private struct Pane(in Rect2 labelRect, in bool border, in bool title)
    {
        public readonly Rect2 LabelRect = labelRect;
        public readonly bool Border = border;
        public readonly bool Title = title;
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
        bool title = !string.IsNullOrWhiteSpace(name);

        ImGui.BeginGroup();

        Vector2 itemSpacing = ImGui.GetStyle().ItemSpacing;

        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, Vector2.Zero);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, Vector2.Zero);

        float frameHeight = ImGui.GetFrameHeight();

        ImGui.BeginGroup();

        Vector2 effectiveSize = size;
        if (size.X < 0f)
        {
            effectiveSize.X = ImGui.GetContentRegionAvail().X;
        }

        ImGui.Dummy(new Vector2(effectiveSize.X - frameHeight * 0.5f, 0f));

        var frameHeightHalf = new Vector2(frameHeight * 0.5f, 0f);

        if (title)
        {
            ImGui.Dummy(frameHeightHalf);
        }

        if (title)
        {
            ImGui.SameLine(0f, 0f);
        }

        ImGui.BeginGroup();

        if (title)
        {
            ImGui.Dummy(frameHeightHalf);
        }

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

        _paneStack.Push(
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
        Pane pane = _paneStack.Pop();

        ImGui.PopItemWidth();

        Vector2 itemSpacing = ImGui.GetStyle().ItemSpacing;

        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, Vector2.Zero);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, Vector2.Zero);

        float frameHeight = ImGui.GetFrameHeight();

        ImGui.EndGroup();
        ImGui.EndGroup();

        ImGui.SameLine(0, 0);
        if (pane.Title)
        {
            ImGui.Dummy(new Vector2(frameHeight * 0.5f, 0f));
        }

        if (pane.Title)
        {
            ImGui.Dummy(new Vector2(0f, frameHeight * 0.5f - itemSpacing.Y));
        }

        ImGui.EndGroup();

        Vector2 itemMin = ImGui.GetItemRectMin();
        Vector2 itemMax = ImGui.GetItemRectMax();

        Rect2 labelRect = pane.LabelRect;
        labelRect.Min.X -= itemSpacing.X;
        labelRect.Max.X += itemSpacing.X;

        if (pane.Border)
        {
            Vector2 halfFrame = pane.Title ? new Vector2(frameHeight * 0.25f, frameHeight) * 0.5f : Vector2.Zero;
            var frameRect = new Rect2(itemMin + halfFrame, itemMax - halfFrame with { Y = -halfFrame.Y });
            for (var i = 0; i < 4; i++)
            {
                switch (i)
                {
                    case 0:
                        ImGui.PushClipRect(new Vector2(-float.MaxValue), labelRect.Min with { Y = float.MaxValue }, true);
                        break;
                    case 1:
                        ImGui.PushClipRect(labelRect.Max with { Y = -float.MaxValue }, new Vector2(float.MaxValue), true);
                        break;
                    case 2:
                        ImGui.PushClipRect(labelRect.Min with { Y = -float.MaxValue }, new Vector2(labelRect.Max.X, labelRect.Min.Y), true);
                        break;
                    case 3:
                        ImGui.PushClipRect(new Vector2(labelRect.Min.X, labelRect.Max.Y), labelRect.Max with { Y = float.MaxValue }, true);
                        break;
                }

                ImDrawListPtr drawList = ImGui.GetWindowDrawList();
                drawList.AddRect(frameRect.Min, frameRect.Max, ImGui.GetColorU32(ImGuiCol.Border), halfFrame.X);

                ImGui.PopClipRect();
            }
        }

        ImGui.PopStyleVar(2);

        if (pane.Title)
        {
            ImGui.Dummy(new Vector2(0f, frameHeight * 0.5f + itemSpacing.Y));
        }

        ImGui.EndGroup();
    }
}
