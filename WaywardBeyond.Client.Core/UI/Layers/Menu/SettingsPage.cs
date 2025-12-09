using System;
using System.Globalization;
using System.Numerics;
using Reef;
using Reef.Constraints;
using Reef.UI;
using Swordfish.Audio;
using Swordfish.Graphics;
using Swordfish.Library.Util;
using Swordfish.Settings;
using WaywardBeyond.Client.Core.Configuration;
using WaywardBeyond.Client.Core.Numerics;

namespace WaywardBeyond.Client.Core.UI.Layers.Menu;

internal sealed class SettingsPage(
    in SettingsManager settingsManager,
    in ControlSettings controlSettings,
    in WindowSettings windowSettings,
    in RenderSettings renderSettings,
    in VolumeSettings volumeSettings,
    in IAudioService audioService
) : IMenuPage<MenuPage>
{
    public MenuPage ID => MenuPage.Settings;

    private readonly SettingsManager _settingsManager = settingsManager;
    private readonly ControlSettings _controlSettings = controlSettings;
    private readonly WindowSettings _windowSettings = windowSettings;
    private readonly RenderSettings _renderSettings = renderSettings;
    private readonly VolumeSettings _volumeSettings = volumeSettings;
    private readonly IAudioService _audioService = audioService;
    
    private readonly FontOptions _buttonFontOptions = new()
    {
        Size = 32,
    };

    public Result RenderPage(double delta, UIBuilder<Material> ui, Menu<MenuPage> menu)
    {
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
            
            bool value = ui.Checkbox(id: "Checkbox_VSync", text: "VSync", isChecked: _renderSettings.VSync, _audioService, _volumeSettings);
            _renderSettings.VSync.Set(value);

            value = ui.Checkbox(id: "Checkbox_Fullscreen", text: "Fullscreen", isChecked: _windowSettings.Mode == WindowMode.Fullscreen, _audioService, _volumeSettings);
            _windowSettings.Mode.Set(value ? WindowMode.Fullscreen : WindowMode.Maximized);
            
            value = ui.Checkbox(id: "Checkbox_MSAA", text: "Anti-aliasing", isChecked: _renderSettings.AntiAliasing == AntiAliasing.MSAA, _audioService, _volumeSettings);
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
            
            ui.NumberControl(
                id: "Control_LookSensitivity",
                text: "Sensitivity",
                _controlSettings.LookSensitivity,
                constraints: new Int2(1, 10),
                display: new Int2(1, 10),
                steps: 9,
                _audioService,
                _volumeSettings,
                OnLookSensitivityChanged
            );
            
            void OnLookSensitivityChanged(int newValue)
            {
                _controlSettings.LookSensitivity.Set(newValue);
            }
            
            using (ui.Element())
            {
                ui.Constraints = new Constraints
                {
                    Height = new Fixed(50),
                };
            }
            
            using (ui.Text("Volume"))
            {
                ui.FontSize = 24;
            }
            
            ui.NumberControl(
                id: "Control_Volume_Master",
                text: "Master",
                _volumeSettings.Master,
                constraints: new Float2(0f, 1f),
                display: new Int2(0, 10),
                steps: 10,
                _audioService,
                _volumeSettings,
                OnMasterVolumeChanged
            );
            
            void OnMasterVolumeChanged(float newValue)
            {
                _volumeSettings.Master.Set(newValue);
            }
            
            ui.NumberControl(
                id: "Control_Volume_Interface",
                text: "Interface",
                _volumeSettings.Interface,
                constraints: new Float2(0f, 1f),
                display: new Int2(0, 10),
                steps: 10,
                _audioService,
                _volumeSettings,
                OnInterfaceVolumeChanged
            );
            
            void OnInterfaceVolumeChanged(float newValue)
            {
                _volumeSettings.Interface.Set(newValue);
            }
            
            ui.NumberControl(
                id: "Control_Volume_Effects",
                text: "Effects",
                _volumeSettings.Effects,
                constraints: new Float2(0f, 1f),
                display: new Int2(0, 10),
                steps: 10,
                _audioService,
                _volumeSettings,
                OnEffectsVolumeChanged
            );
            
            void OnEffectsVolumeChanged(float newValue)
            {
                _volumeSettings.Effects.Set(newValue);
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

            if (ui.TextButton(id: "Button_Back", text: "Back", _buttonFontOptions, _audioService, _volumeSettings))
            {
                _settingsManager.ApplySettings();
                menu.GoBack();
            }
        }
        
        return Result.FromSuccess();
    }
}