using System;
using System.Numerics;
using Reef;
using Reef.Constraints;
using Reef.UI;
using Swordfish.Graphics;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Core.Configuration;

namespace WaywardBeyond.Client.Core.UI.Layers.Menu;

internal sealed class ControlSettingsPage(in ControlSettings controlSettings) : IMenuPage<MenuPage>
{
    private const string INCREASE_UNICODE = "\uf0fe";
    private const string DECREASE_UNICODE = "\uf146";
    
    public MenuPage ID => MenuPage.ControlSettings;

    private readonly ControlSettings _controlSettings = controlSettings;
    private readonly FontOptions _buttonFontOptions = new()
    {
        Size = 32,
    };

    public Result RenderPage(double delta, UIBuilder<Material> ui, Menu<MenuPage> menu)
    {
        int currentSensitivity = _controlSettings.LookSensitivity;
        
        using (ui.Element())
        {
            ui.Constraints = new Constraints
            {
                Anchors = Anchors.Center | Anchors.Bottom,
                X = new Relative(0.5f),
                Y = new Relative(0.99f),
            };

            if (ui.TextButton(id: "Button_Back", text: "Back", _buttonFontOptions))
            {
                menu.GoBack();
            }
        }
        
        using (ui.Element())
        {
            ui.LayoutDirection = LayoutDirection.Vertical;
            ui.Constraints = new Constraints
            {
                Anchors = Anchors.Center | Anchors.Top,
                X = new Relative(0.5f),
                Y = new Relative(0.3f),
                Width = new Fixed(250),
                Height = new Relative(0.5f),
            };
            
            using (ui.Element())
            {
                ui.LayoutDirection = LayoutDirection.Horizontal;
                ui.Spacing = 8;
                ui.Constraints = new Constraints
                {
                    Width = new Fill(),
                };
            
                using (ui.Text("Sensitivity"))
                {
                    ui.FontSize = 20;
                    ui.Color = new Vector4(0.65f, 0.65f, 0.65f, 1f);
                    ui.Constraints = new Constraints
                    {
                        Anchors = Anchors.Center | Anchors.Left,
                        Y = new Relative(0.5f),
                    };
                }

                using (ui.Element())
                {
                    ui.Constraints = new Constraints
                    {
                        Width = new Fill(),
                        Height = new Fill(),
                    };
                }

                using (ui.Element("Button_Decrease_LookSensitivity"))
                {
                    bool clicked = ui.Clicked();
                    bool hovering = ui.Hovering();
                
                    using (ui.Text(DECREASE_UNICODE, fontID: "Font Awesome 6 Free Regular"))
                    {
                        ui.FontSize = 30;
                    
                        //  TODO swordfish#233 For some reason some FA glyphs are rendering outside of their bounds
                        ui.Padding = new Padding
                        {
                            Right = 4,
                            Bottom = 12,
                        };
                    
                        if (clicked)
                        {
                            ui.Color = new Vector4(0f, 0f, 0f, 1f);
                            _controlSettings.LookSensitivity.Set(Math.Clamp(currentSensitivity - 1, 1, 10));
                        }
                        else if (hovering)
                        {
                            ui.Color = new Vector4(1f, 1f, 1f, 1f);
                        }
                        else
                        {
                            ui.Color = new Vector4(0.65f, 0.65f, 0.65f, 1f);
                        }
                    }
                }

                using (ui.Text(_controlSettings.LookSensitivity.Get().ToString()))
                {
                    ui.FontSize = 20;
                    ui.Constraints = new Constraints
                    {
                        Anchors = Anchors.Center | Anchors.Left,
                        Y = new Relative(0.5f),
                    };
                }
                
                using (ui.Element("Button_Increase_LookSensitivity"))
                {
                    bool clicked = ui.Clicked();
                    bool hovering = ui.Hovering();
                
                    using (ui.Text(INCREASE_UNICODE, fontID: "Font Awesome 6 Free Regular"))
                    {
                        ui.FontSize = 30;
                    
                        //  TODO swordfish#233 For some reason some FA glyphs are rendering outside of their bounds
                        ui.Padding = new Padding
                        {
                            Right = 4,
                            Bottom = 12,
                        };
                    
                        if (clicked)
                        {
                            ui.Color = new Vector4(0f, 0f, 0f, 1f);
                            _controlSettings.LookSensitivity.Set(Math.Clamp(currentSensitivity + 1, 1, 10));
                        }
                        else if (hovering)
                        {
                            ui.Color = new Vector4(1f, 1f, 1f, 1f);
                        }
                        else
                        {
                            ui.Color = new Vector4(0.65f, 0.65f, 0.65f, 1f);
                        }
                    }
                }
            }
        }
        
        return Result.FromSuccess();
    }
}