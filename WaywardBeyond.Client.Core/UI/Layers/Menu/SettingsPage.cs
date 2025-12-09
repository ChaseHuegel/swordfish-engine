using System;
using System.Numerics;
using Reef;
using Reef.Constraints;
using Reef.UI;
using Swordfish.Audio;
using Swordfish.Graphics;
using Swordfish.Library.Util;
using Swordfish.Settings;
using WaywardBeyond.Client.Core.Configuration;

namespace WaywardBeyond.Client.Core.UI.Layers.Menu;

internal sealed class SettingsPage(
    in SettingsManager settingsManager,
    in ControlSettings controlSettings,
    in WindowSettings windowSettings,
    in RenderSettings renderSettings,
    in IAudioService audioService
) : IMenuPage<MenuPage>
{
    private const string INCREASE_UNICODE = "\uf0fe";
    private const string DECREASE_UNICODE = "\uf146";
    
    public MenuPage ID => MenuPage.Settings;

    private readonly ControlSettings _controlSettings = controlSettings;
    private readonly WindowSettings _windowSettings = windowSettings;
    private readonly RenderSettings _renderSettings = renderSettings;
    private readonly SettingsManager _settingsManager = settingsManager;
    private readonly IAudioService _audioService = audioService;
    
    private readonly FontOptions _buttonFontOptions = new()
    {
        Size = 32,
    };

    public Result RenderPage(double delta, UIBuilder<Material> ui, Menu<MenuPage> menu)
    {
        int currentSensitivity = _controlSettings.LookSensitivity;
        
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

            using (ui.Text("Display"))
            {
                ui.FontSize = 24;
            }
            
            bool value = ui.Checkbox(id: "Checkbox_VSync", text: "VSync", isChecked: _renderSettings.VSync, _audioService);
            _renderSettings.VSync.Set(value);

            value = ui.Checkbox(id: "Checkbox_Fullscreen", text: "Fullscreen", isChecked: _windowSettings.Mode == WindowMode.Fullscreen, _audioService);
            _windowSettings.Mode.Set(value ? WindowMode.Fullscreen : WindowMode.Maximized);
            
            value = ui.Checkbox(id: "Checkbox_MSAA", text: "Anti-aliasing", isChecked: _renderSettings.AntiAliasing == AntiAliasing.MSAA, _audioService);
            _renderSettings.AntiAliasing.Set(value ? AntiAliasing.MSAA : AntiAliasing.None);
            
            using (ui.Element())
            {
                ui.Constraints = new Constraints
                {
                    Height = new Fixed(50),
                };
            }
            
            using (ui.Text("Controls"))
            {
                ui.FontSize = 24;
            }
            
            using (ui.Element())
            {
                ui.Spacing = 8;
                ui.LayoutDirection = LayoutDirection.Horizontal;
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
                    
                    Widgets.Interactions interactions = clicked ? Widgets.Interactions.Click : Widgets.Interactions.None;
                    interactions.WithButtonAudio(_audioService);
                    
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
                    
                    Widgets.Interactions interactions = clicked ? Widgets.Interactions.Click : Widgets.Interactions.None;
                    interactions.WithButtonAudio(_audioService);
                    
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
        
        using (ui.Element())
        {
            ui.Constraints = new Constraints
            {
                Anchors = Anchors.Center | Anchors.Bottom,
                X = new Relative(0.5f),
                Y = new Relative(0.99f),
            };

            if (ui.TextButton(id: "Button_Back", text: "Back", _buttonFontOptions, _audioService))
            {
                _settingsManager.ApplySettings();
                menu.GoBack();
            }
        }
        
        return Result.FromSuccess();
    }
}