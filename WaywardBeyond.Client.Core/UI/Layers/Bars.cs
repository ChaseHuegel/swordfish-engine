using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Numerics;
using Reef;
using Reef.Constraints;
using Reef.UI;
using Swordfish.Graphics;
using Swordfish.Library.Extensions;
using Swordfish.Library.Util;

namespace WaywardBeyond.Client.Core.UI.Layers;

internal class Bars(in NotificationService notificationService)
{
    private readonly NotificationService _notificationService = notificationService;
    private readonly Vector4 _backgroundColor = Color.FromArgb(int.Parse("FF4F546B", NumberStyles.HexNumber)).ToVector4();
    private readonly Vector4 _slotColor = Color.FromArgb(int.Parse("FF3978A8", NumberStyles.HexNumber)).ToVector4();
    private readonly Vector4 _selectedColor = Color.FromArgb(int.Parse("FF8AEBF1", NumberStyles.HexNumber)).ToVector4();
    
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
                            ui.Color = _backgroundColor;
                            ui.Padding = new Padding(2);
                            ui.Constraints = new Constraints
                            {
                                Width = new Fixed(512),
                                Height = new Fixed(20),
                            };
                            
                            using (ui.Element())
                            {
                                ui.Color = _selectedColor;
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