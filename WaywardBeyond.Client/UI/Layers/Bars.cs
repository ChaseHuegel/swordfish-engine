using System;
using System.Collections.Generic;
using System.Numerics;
using Reef;
using Reef.Constraints;
using Reef.UI;
using Swordfish.Graphics;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Extensions;
using WaywardBeyond.Client.UI.Theme;
using WaywardBeyond.Data.Networking;

namespace WaywardBeyond.Client.UI.Layers;

/// <summary>Renders top-center progress bar notifications such as skill XP telemetry.</summary>
internal sealed class Bars(in NotificationService notificationService)
{
    private const int BAR_WIDTH = 512;
    private const int TRACK_HEIGHT = 6;

    private readonly NotificationService _notificationService = notificationService;
    private readonly HashSet<string> _seenNotificationIDs = [];

    /// <summary>Returns whether the progress bars container is visible.</summary>
    public bool IsVisible() => true;

    /// <summary>Renders active bar notifications deduplicated by notification ID.</summary>
    public Result RenderUI(double delta, UIBuilder<Material> ui)
    {
        IReadOnlyList<NotificationState> activeStates = _notificationService.GetActiveNotifications();
        if (activeStates.Count == 0)
        {
            return Result.FromSuccess();
        }

        _seenNotificationIDs.Clear();
        DateTime now = DateTime.Now;

        using (ui.Element())
        {
            ui.Passthrough = true;
            ui.Spacing = 8;
            ui.LayoutDirection = LayoutDirection.Vertical;
            ui.Constraints = new Constraints
            {
                Anchors = Anchors.Center,
            };

            for (var i = 0; i < activeStates.Count; i++)
            {
                NotificationState state = activeStates[i];
                if (state.Notification.Type != NotificationType.Bar)
                {
                    continue;
                }

                if (!_seenNotificationIDs.Add(state.Notification.ID))
                {
                    continue;
                }

                float alpha = state.GetAlpha(now);
                var fade = new Vector4(1f, 1f, 1f, alpha);

                using (ui.Element())
                {
                    ui.Passthrough = true;
                    ui.Spacing = 6;
                    ui.Padding = new Padding(left: 12, top: 8, right: 12, bottom: 8);
                    ui.LayoutDirection = LayoutDirection.Vertical;
                    ui.Color = UiTheme.PanelBackground * fade;
                    ui.Border = new BorderStyle
                    {
                        Thickness = UiMetrics.PanelBorderThickness,
                        Color = UiTheme.BorderInner
                    };
                    ui.Constraints = new Constraints
                    {
                        Anchors = Anchors.Center,
                        Width = new Fixed(BAR_WIDTH),
                    };

                    using (ui.Text(state.Notification.Text, UiMetrics.SmallFont))
                    {
                        ui.Passthrough = true;
                        ui.Color = UiTheme.Text * fade;
                        ui.Constraints = new Constraints
                        {
                            Anchors = Anchors.Left,
                        };
                    }

                    using (ui.Element())
                    {
                        ui.Passthrough = true;
                        ui.LayoutDirection = LayoutDirection.None;
                        ui.Color = UiTheme.Track * fade;
                        ui.Constraints = new Constraints
                        {
                            Width = new Relative(1f),
                            Height = new Fixed(TRACK_HEIGHT),
                        };

                        using (ui.Element())
                        {
                            ui.Passthrough = true;
                            ui.Color = UiTheme.Accent * fade;
                            ui.Constraints = new Constraints
                            {
                                Width = new Relative(Math.Clamp(state.Notification.Amount, 0f, 1f)),
                                Height = new Relative(1f),
                            };
                        }
                    }
                }
            }
        }

        return Result.FromSuccess();
    }
}
