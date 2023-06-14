using System;
using Swordfish.ECS;
using Swordfish.Graphics;
using Swordfish.Library.Constraints;
using Swordfish.Library.Diagnostics;
using Swordfish.Library.Types;
using Swordfish.Types.Constraints;
using Swordfish.UI.Elements;

namespace Swordfish.Editor.UI;

public class StatsWindow : CanvasElement
{
    public StatsWindow(IWindowContext windowContext, IECSContext ecsContext) : base("Stats")
    {
        Flags = ImGuiNET.ImGuiWindowFlags.NoResize | ImGuiNET.ImGuiWindowFlags.NoCollapse | ImGuiNET.ImGuiWindowFlags.NoBackground | ImGuiNET.ImGuiWindowFlags.NoTitleBar;

        Constraints = new RectConstraints {
            Anchor = ConstraintAnchor.TOP_LEFT,
            X = new RelativeConstraint(0.15f),
            Y = new AbsoluteConstraint(0),
            Width = new AbsoluteConstraint(200),
            Height = new AbsoluteConstraint(200)
        };

        AddDeltaDisplay("Main Delta", ref windowContext.UpdateDelta.Changed);
        AddDeltaDisplay("Render Delta", ref windowContext.RenderDelta.Changed);
        AddDeltaDisplay("ECS Delta", ref ecsContext.Delta.Changed);

        Content.Add(new DividerElement());

        AddDeltaToMsDisplay("Main Frametime", ref windowContext.UpdateDelta.Changed);
        AddDeltaToMsDisplay("Render Frametime", ref windowContext.RenderDelta.Changed);
        AddDeltaToMsDisplay("ECS Frametime", ref ecsContext.Delta.Changed);

        Content.Add(new DividerElement());

        AddDeltaToFramerateDisplay("Main FPS", ref windowContext.UpdateDelta.Changed);
        AddDeltaToFramerateDisplay("Render FPS", ref windowContext.RenderDelta.Changed);
        AddDeltaToFramerateDisplay("ECS FPS", ref ecsContext.Delta.Changed);
    }

    private void AddDeltaToFramerateDisplay(string title, ref EventHandler<DataChangedEventArgs<double>> statHandler)
    {
        TextElement displayElement = new(title);

        Sampler sampler = new();
        statHandler += (sender, e) =>
        {
            double value = 1000d / (e.NewValue * 1000d);
            sampler.Record(value);
            displayElement.Label = sampler.Average.ToString("F0");
        };

        Content.Add(displayElement);
    }

    private void AddDeltaToMsDisplay(string title, ref EventHandler<DataChangedEventArgs<double>> statHandler)
    {
        TextElement displayElement = new(title);

        Sampler sampler = new();
        statHandler += (sender, e) =>
        {
            double value = e.NewValue * 1000d;
            sampler.Record(value);
            displayElement.Label = sampler.Average.ToString("F2") + "ms";
        };

        Content.Add(displayElement);
    }

    private void AddDeltaDisplay(string title, ref EventHandler<DataChangedEventArgs<double>> statHandler) {
        TextElement displayElement = new(title);

        Sampler sampler = new();
        statHandler += (sender, e) =>
        {
            sampler.Record(e.NewValue);
            displayElement.Label = sampler.Average.ToString("F4");
        };

        Content.Add(displayElement);
    }
}