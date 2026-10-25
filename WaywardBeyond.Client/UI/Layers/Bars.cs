using System.Collections.Generic;
using System.Linq;
using Reef;
using Reef.Constraints;
using Reef.UI;
using Swordfish.Graphics;
using Swordfish.Library.Extensions;
using Swordfish.Library.Util;
using WaywardBeyond.Client.UI.Theme;
using WaywardBeyond.Networking;
using WaywardBeyond.Data.Networking;

namespace WaywardBeyond.Client.UI.Layers;

internal class Bars(in NotificationService notificationService)
{
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
            
            //  Render the bar notifications
            using (ui.Element())
            {
                ui.Spacing = 10;
                ui.LayoutDirection = LayoutDirection.Vertical;
                ui.Constraints = new Constraints
                {
                    Anchors = Anchors.Center,
                };

                IEnumerable<NotificationState> notificationStates = _notificationService.GetActiveNotifications(NotificationType.Bar)
                    .DistinctBy(state => state.Notification.ID);
                
                foreach (NotificationState state in notificationStates)
                {
                    using (ui.Element())
                    {
                        ui.Spacing = 4;
                        ui.LayoutDirection = LayoutDirection.Vertical;

                        using (ui.Text(state.Notification.Text))
                        {
                            ui.Constraints = new Constraints
                            {
                                Anchors = Anchors.Center,
                            };
                        }

                        using (ui.Element())
                        {
                            ui.LayoutDirection = LayoutDirection.None;
                            ui.Color = UiTheme.SlotBackground;
                            ui.Padding = new Padding(4);
                            ui.Constraints = new Constraints
                            {
                                Width = new Fixed(512),
                                Height = new Fixed(16),
                            };
                            
                            using (ui.Element())
                            {
                                ui.Color = UiTheme.SlotSelected;
                                ui.Constraints = new Constraints
                                {
                                    Width = new Relative(state.Notification.Amount),
                                    Height = new Relative(1f),
                                };
                            }
                        }
                    }
                }
            }
        }

        return Result.FromSuccess();
    }
}
