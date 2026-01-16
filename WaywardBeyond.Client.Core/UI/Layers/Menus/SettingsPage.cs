using Reef;
using Reef.Constraints;
using Reef.UI;
using Swordfish.Audio;
using Swordfish.Graphics;
using Swordfish.Library.Globalization;
using Swordfish.Library.Util;
using Swordfish.Settings;
using WaywardBeyond.Client.Core.Configuration;
using WaywardBeyond.Client.Core.Numerics;

namespace WaywardBeyond.Client.Core.UI.Layers.Menus;

internal abstract class SettingsPage<TIdentifier>(
    in SettingsManager settingsManager,
    in ControlSettings controlSettings,
    in WindowSettings windowSettings,
    in RenderSettings renderSettings,
    in VolumeSettings volumeSettings,
    in IAudioService audioService,
    in ILocalization localization
) : IMenuPage<TIdentifier> where TIdentifier : notnull
{
    private readonly SettingsManager _settingsManager = settingsManager;
    private readonly ControlSettings _controlSettings = controlSettings;
    private readonly WindowSettings _windowSettings = windowSettings;
    private readonly RenderSettings _renderSettings = renderSettings;
    private readonly VolumeSettings _volumeSettings = volumeSettings;
    private readonly IAudioService _audioService = audioService;
    private readonly ILocalization _localization = localization;
    
    private readonly FontOptions _buttonFontOptions = new()
    {
        Size = 32,
    };

    public abstract TIdentifier ID { get; }

    public Result RenderPage(double delta, UIBuilder<Material> ui, Menu<TIdentifier> menu)
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
            
            using (ui.Text(_localization.GetString("ui.menu.display")!))
            {
                ui.FontSize = 24;
            }
            
            bool value = ui.Checkbox(id: "Checkbox_VSync", text: _localization.GetString("ui.setting.vsync")!, isChecked: _renderSettings.VSync, _audioService, _volumeSettings);
            _renderSettings.VSync.Set(value);
            
            value = ui.Checkbox(id: "Checkbox_Fullscreen", text: _localization.GetString("ui.setting.fullscreen")!, isChecked: _windowSettings.Mode == WindowMode.Fullscreen, _audioService, _volumeSettings);
            _windowSettings.Mode.Set(value ? WindowMode.Fullscreen : WindowMode.Maximized);
            
            value = ui.Checkbox(id: "Checkbox_MSAA", text: _localization.GetString("ui.setting.msaa")!, isChecked: _renderSettings.AntiAliasing == AntiAliasing.MSAA, _audioService, _volumeSettings);
            _renderSettings.AntiAliasing.Set(value ? AntiAliasing.MSAA : AntiAliasing.None);
            
            ui.NumberControl(
                id: "Control_FOV",
                text: _localization.GetString("ui.setting.fov")!,
                _renderSettings.FOV,
                constraints: new Int2(60, 120),
                display: new Int2(60, 120),
                steps: 12,
                _audioService,
                _volumeSettings,
                OnFOVChanged
            );
            
            using (ui.Element())
            {
                ui.Constraints = new Constraints
                {
                    Height = new Fixed(50),
                };
            }
            
            using (ui.Text(_localization.GetString("ui.menu.controls")!))
            {
                ui.FontSize = 24;
            }
            
            ui.NumberControl(
                id: "Control_LookSensitivity",
                text: _localization.GetString("ui.setting.mouseSensitivity")!,
                _controlSettings.LookSensitivity,
                constraints: new Int2(1, 10),
                display: new Int2(1, 10),
                steps: 9,
                _audioService,
                _volumeSettings,
                OnLookSensitivityChanged
            );

            using (ui.Element())
            {
                ui.Constraints = new Constraints
                {
                    Height = new Fixed(50),
                };
            }
            
            using (ui.Text(_localization.GetString("ui.menu.volume")!))
            {
                ui.FontSize = 24;
            }
            
            ui.NumberControl(
                id: "Control_Volume_Master",
                text: _localization.GetString("ui.setting.volume.master")!,
                _volumeSettings.Master,
                constraints: new Float2(0f, 1f),
                display: new Int2(0, 10),
                steps: 10,
                _audioService,
                _volumeSettings,
                OnMasterVolumeChanged
            );

            ui.NumberControl(
                id: "Control_Volume_Interface",
                text: _localization.GetString("ui.setting.volume.interface")!,
                _volumeSettings.Interface,
                constraints: new Float2(0f, 1f),
                display: new Int2(0, 10),
                steps: 10,
                _audioService,
                _volumeSettings,
                OnInterfaceVolumeChanged
            );

            ui.NumberControl(
                id: "Control_Volume_Effects",
                text: _localization.GetString("ui.setting.volume.effects")!,
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

            if (ui.TextButton(id: "Button_Back", text: _localization.GetString("ui.button.back")!, _buttonFontOptions, _audioService, _volumeSettings))
            {
                _settingsManager.ApplySettings();
                menu.GoBack();
            }
        }
        
        return Result.FromSuccess();
    }
    
    private void OnFOVChanged(int newValue)
    {
        _renderSettings.FOV.Set(newValue);
    }
    
    private void OnLookSensitivityChanged(int newValue)
    {
        _controlSettings.LookSensitivity.Set(newValue);
    }

    private void OnMasterVolumeChanged(float newValue)
    {
        _volumeSettings.Master.Set(newValue);
    }

    private void OnInterfaceVolumeChanged(float newValue)
    {
        _volumeSettings.Interface.Set(newValue);
    }
}