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
    private const int PADDING_X = 12;
    private const int PADDING_Y = 8;
    private const int INNER_WIDTH = BAR_WIDTH - PADDING_X * 2;
    private const int HEADER_HEIGHT = 18;
    private const int TRACK_HEIGHT = 6;
    private const int SEGMENT_GAP = 2;

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

                RenderBar(ui, in state.Notification, state.GetAlpha(now));
            }
        }

        return Result.FromSuccess();
    }

    private static void RenderBar(UIBuilder<Material> ui, in Notification notification, float alpha)
    {
        var fade = new Vector4(1f, 1f, 1f, alpha);
        Vector4 fillColor = notification.Color ?? UiTheme.Accent;

        using (ui.Element())
        {
            ui.LayoutDirection = LayoutDirection.Vertical;
            
            using (ui.Element())
            {
                ui.Passthrough = true;
                ui.Spacing = 6;
                ui.Padding = new Padding(left: PADDING_X, top: PADDING_Y, right: PADDING_X, bottom: PADDING_Y);
                ui.LayoutDirection = LayoutDirection.Vertical;
                ui.Color = UiTheme.PanelBackground * fade;
                ui.Constraints = new Constraints
                {
                    Anchors = Anchors.Center,
                    Width = new Fixed(BAR_WIDTH),
                };

                using (ui.Element())
                {
                    ui.Passthrough = true;
                    ui.LayoutDirection = LayoutDirection.None;
                    ui.Constraints = new Constraints
                    {
                        Width = new Fixed(INNER_WIDTH),
                        Height = new Fixed(HEADER_HEIGHT),
                    };

                    using (ui.Text(notification.Text, UiMetrics.SmallFont))
                    {
                        ui.Passthrough = true;
                        ui.Color = UiTheme.Text * fade;
                        ui.Constraints = new Constraints
                        {
                            Anchors = Anchors.Left | Anchors.Center,
                        };
                    }

                    if (!string.IsNullOrEmpty(notification.Subtext))
                    {
                        using (ui.Text(notification.Subtext, UiMetrics.SmallFont))
                        {
                            ui.Passthrough = true;
                            ui.Color = UiTheme.Text * fade;
                            ui.Constraints = new Constraints
                            {
                                Anchors = Anchors.Right | Anchors.Center,
                            };
                        }
                    }
                }

                using (ui.Element())
                {
                    ui.Passthrough = true;
                    ui.LayoutDirection = LayoutDirection.None;
                    ui.Color = UiTheme.Track * fade;
                    ui.Constraints = new Constraints
                    {
                        Width = new Fixed(INNER_WIDTH),
                        Height = new Fixed(TRACK_HEIGHT),
                    };

                    using (ui.Element())
                    {
                        ui.Passthrough = true;
                        ui.Color = fillColor * fade;
                        ui.Constraints = new Constraints
                        {
                            Width = new Relative(Math.Clamp(notification.Amount, 0f, 1f)),
                            Height = new Relative(1f),
                        };
                    }

                    if (notification.Segments is > 1)
                    {
                        int segments = notification.Segments.Value;
                        for (var segmentIndex = 1; segmentIndex < segments; segmentIndex++)
                        {
                            int dividerX = INNER_WIDTH * segmentIndex / segments - SEGMENT_GAP / 2;
                            using (ui.Element())
                            {
                                ui.Passthrough = true;
                                ui.Color = UiTheme.PanelBackground * fade;
                                ui.Constraints = new Constraints
                                {
                                    X = new Fixed(dividerX),
                                    Y = new Fixed(0),
                                    Width = new Fixed(SEGMENT_GAP),
                                    Height = new Fixed(TRACK_HEIGHT),
                                };
                            }
                        }
                    }
                }
            }
            
            using (ui.Element())
            {
                ui.Color = UiTheme.Border;
                ui.Constraints = new Constraints
                {
                    Width = new Fill(),
                    Height = new Fixed(1)
                };
            }
        }
    }
}
