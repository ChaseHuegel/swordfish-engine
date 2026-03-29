using System;
using Reef;
using Reef.UI;
using Swordfish.Graphics;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Core.Extensions;

namespace WaywardBeyond.Client.Core.UI.Layers;

internal class Actions(
    in IActionIndicator[] actionIndicators,
    in NotificationService notificationService
) {
    private readonly IActionIndicator[] _actionIndicators = actionIndicators;
    private readonly NotificationService _notificationService = notificationService;

    public bool IsVisible()
    {
        return true;
    }

    public Result RenderUI(double delta, UIBuilder<Material> ui)
    {
        using (ui.Element())
        {
            ui.Spacing = 20;
            ui.LayoutDirection = LayoutDirection.Vertical;
            ui.Constraints = new Constraints
            {
                Anchors = Anchors.Center,
            };
            
            //  Render the latest action notification
            using (ui.Element())
            {
                ui.Constraints = new Constraints
                {
                    Anchors = Anchors.Center,
                };
                
                foreach (NotificationState state in _notificationService.GetActiveNotifications(NotificationType.Action))
                {
                    DateTime now = DateTime.Now;
                    state.Render(ui, now, hasBackground: false);
                    break;
                }
            }

            //  Render indicators
            using (ui.Element())
            {
                ui.LayoutDirection = LayoutDirection.Horizontal;
                ui.Spacing = 20;
                ui.Constraints = new Constraints
                {
                    Anchors = Anchors.Center,
                };

                for (var i = 0; i < _actionIndicators.Length; i++)
                {
                    IActionIndicator actionIndicator = _actionIndicators[i];
                    if (actionIndicator.IsVisible())
                    {
                        //  TODO handle non-success results
                        actionIndicator.RenderIndicator(delta, ui);
                    }
                }
            }
        }

        return Result.FromSuccess();
    }
}