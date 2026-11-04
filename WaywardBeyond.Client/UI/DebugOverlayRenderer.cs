using System;
using System.Numerics;
using Microsoft.Extensions.Logging;
using Reef;
using Reef.UI;
using Swordfish.Graphics;
using Swordfish.Library.IO;
using Swordfish.Library.Util;
using Swordfish.UI.Reef;
using WaywardBeyond.Client.Configuration;

namespace WaywardBeyond.Client.UI;

internal class DebugOverlayRenderer : IUILayer
{
    private readonly ILogger _logger;
    private readonly ReefContext _reefContext;
    private readonly DebugSettings _debugSettings;
    private readonly Swordfish.Settings.DebugSettings _engineDebugSettings;
    private readonly IDebugOverlay[] _overlays;

    public DebugOverlayRenderer(
        ILogger<DebugOverlayRenderer> logger,
        ReefContext reefContext,
        IShortcutService shortcutService,
        DebugSettings debugSettings,
        Swordfish.Settings.DebugSettings engineDebugSettings,
        IDebugOverlay[] overlays
    ) {
        _logger = logger;
        _reefContext = reefContext;
        _debugSettings = debugSettings;
        _engineDebugSettings = engineDebugSettings;
        _overlays = overlays;

        var toggleShortcut = new Shortcut
        {
            Name = "Toggle debug",
            Category = "Developer",
            Key = Key.F3,
            IsEnabled = Shortcut.DefaultEnabled,
            Action = OnToggleDebug,
        };
        shortcutService.RegisterShortcut(toggleShortcut);

        var toggleUIDebugShortcut = new Shortcut
        {
            Name = "Toggle UI debug",
            Category = "Developer/Debug",
            Key = Key.F4,
            IsEnabled = IsVisible,
            Action = OnToggleUIDebug,
        };
        shortcutService.RegisterShortcut(toggleUIDebugShortcut);

        var toggleVoxelGizmosShortcut = new Shortcut
        {
            Name = "Toggle voxel gizmos",
            Category = "Developer/Debug",
            Key = Key.F2,
            IsEnabled = IsVisible,
            Action = OnToggleVoxelGizmos,
        };
        shortcutService.RegisterShortcut(toggleVoxelGizmosShortcut);

        var cycleSaoDebugShortcut = new Shortcut
        {
            Name = "Cycle SAO debug",
            Category = "Developer/Debug",
            Key = Key.F6,
            IsEnabled = IsVisible,
            Action = OnCycleSaoDebug,
        };
        shortcutService.RegisterShortcut(cycleSaoDebugShortcut);
    }

    public bool IsVisible()
    {
        return _debugSettings.OverlayVisible;
    }

    public Result RenderUI(double delta, UIBuilder<Material> ui)
    {
        using (ui.Element())
        {
            ui.LayoutDirection = LayoutDirection.Vertical;
            ui.Padding = new Padding
            {
                Left = 20,
                Top = 20,
                Right = 20,
                Bottom = 20,
            };

            SaoDebugView saoDebugView = CurrentSaoDebugView();
            if (saoDebugView != SaoDebugView.Off)
            {
                using (ui.Element())
                {
                    ui.Color = new Vector4(0f, 0f, 0f, 128f / 255f);
                    using (ui.Text($"SAO debug: {saoDebugView}"));
                }
            }

            for (var i = 0; i < _overlays.Length; i++)
            {
                IDebugOverlay overlay = _overlays[i];
                if (!overlay.IsVisible())
                {
                    continue;
                }

                try
                {
                    using (ui.Element())
                    {
                        ui.Color = new Vector4(0f, 0f, 0f, 128f / 255f);
                        ui.LayoutDirection = LayoutDirection.Vertical;

                        Result result = overlay.RenderDebugOverlay(delta, _reefContext.Builder);
                        if (!result)
                        {
                            _logger.LogError(result, "Failed to render debug overlay \"{overlay}\".", overlay.GetType());
                        }
                    }
                }
                catch (Exception exception)
                {
                    _logger.LogError(exception, "Caught an exception when rendering debug overlay \"{overlay}\".", overlay.GetType());
                }
            }
        }

        return Result.FromSuccess();
    }

    private void OnToggleDebug()
    {
        _debugSettings.OverlayVisible.Set(!_debugSettings.OverlayVisible);
        _debugSettings.VoxelGizmosVisible.Set(false);
        _engineDebugSettings.UI.Set(false);
        ApplySaoDebugView(SaoDebugView.Off);
    }

    private void OnToggleUIDebug()
    {
        _engineDebugSettings.UI.Set(!_engineDebugSettings.UI);
    }

    private void OnToggleVoxelGizmos()
    {
        _debugSettings.VoxelGizmosVisible.Set(!_debugSettings.VoxelGizmosVisible);
    }

    private void OnCycleSaoDebug()
    {
        ApplySaoDebugView(CurrentSaoDebugView().Next());
    }

    private SaoDebugView CurrentSaoDebugView()
    {
        if (!_engineDebugSettings.SAO.Get())
        {
            return SaoDebugView.Off;
        }

        if (_engineDebugSettings.SAODepth.Get())
        {
            return SaoDebugView.Depth;
        }

        return _engineDebugSettings.SAORaw.Get()
            ? SaoDebugView.Raw
            : SaoDebugView.Blurred;
    }

    private void ApplySaoDebugView(SaoDebugView view)
    {
        _engineDebugSettings.SAO.Set(view != SaoDebugView.Off);
        _engineDebugSettings.SAORaw.Set(view == SaoDebugView.Raw);
        _engineDebugSettings.SAODepth.Set(view == SaoDebugView.Depth);
    }
}
