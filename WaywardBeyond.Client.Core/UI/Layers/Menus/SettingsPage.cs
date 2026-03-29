using Reef;
using Reef.Constraints;
using Reef.UI;
using Swordfish.Graphics;
using Swordfish.Library.Globalization;
using Swordfish.Library.Util;
using Swordfish.Settings;
using WaywardBeyond.Client.Core.Configuration;
using WaywardBeyond.Client.Core.Numerics;
using WaywardBeyond.Client.Core.Services;

namespace WaywardBeyond.Client.Core.UI.Layers.Menus;

internal abstract class SettingsPage<TIdentifier>(
    in SettingsManager settingsManager,
    in ControlSettings controlSettings,
    in WindowSettings windowSettings,
    in RenderSettings renderSettings,
    in VolumeSettings volumeSettings,
    in GameplaySettings gameplaySettings,
    in SoundEffectService soundEffectService,
    in ILocalization localization
) : IMenuPage<TIdentifier> where TIdentifier : notnull
{
    private readonly SettingsManager _settingsManager = settingsManager;
    private readonly ControlSettings _controlSettings = controlSettings;
    private readonly WindowSettings _windowSettings = windowSettings;
    private readonly RenderSettings _renderSettings = renderSettings;
    private readonly VolumeSettings _volumeSettings = volumeSettings;
    private readonly GameplaySettings _gameplaySettings = gameplaySettings;
    private readonly SoundEffectService _soundEffectService = soundEffectService;
    private readonly ILocalization _localization = localization;

    private readonly Widgets.ButtonOptions _buttonOptions = new(
        new FontOptions {
            Size = 32,
        },
        new Widgets.AudioOptions(soundEffectService)
    );

    public abstract TIdentifier ID { get; }

    public Result RenderPage(double delta, UIBuilder<Material> ui, Menu<TIdentifier> menu)
    {
        using (ui.Element())
        {
            ui.Constraints = new Constraints
            {
                Anchors = Anchors.Center,
            };
        
            using (ui.Text(_localization.GetString("ui.menu.controls")!))
            {
                ui.FontSize = 24;
            }
        }
        
        using (ui.Element())
        {
            ui.LayoutDirection = LayoutDirection.Vertical;
            ui.Constraints = new Constraints
            {
                Anchors = Anchors.Center,
                Width = new Fixed(300),
            };
            
            bool rememberShape = ui.Checkbox(id: "Checkbox_RememberShape", text: _localization.GetString("ui.setting.rememberShape")!, isChecked: _controlSettings.RememberShape, _soundEffectService);
            _controlSettings.RememberShape.Set(rememberShape);
            
            bool rememberOrientation = ui.Checkbox(id: "Checkbox_RememberOrientation", text: _localization.GetString("ui.setting.rememberOrientation")!, isChecked: _controlSettings.RememberOrientation, _soundEffectService);
            _controlSettings.RememberOrientation.Set(rememberOrientation);
            
            ui.NumberControl(
                id: "Control_LookSensitivity",
                text: _localization.GetString("ui.setting.mouseSensitivity")!,
                _controlSettings.LookSensitivity,
                constraints: new Int2(1, 10),
                display: new Int2(1, 10),
                steps: 9,
                _soundEffectService,
                OnLookSensitivityChanged
            );
        }
                
        using (ui.Element())
        {
            ui.Constraints = new Constraints
            {
                Width = new Fill(),
                Height = new Fill(),
            };
        }
        
        using (ui.Element())
        {
            ui.Constraints = new Constraints
            {
                Anchors = Anchors.Center,
            };

            using (ui.Text(_localization.GetString("ui.menu.gameplay")!))
            {
                ui.FontSize = 24;
            }
        }

        using (ui.Element())
        {
            ui.LayoutDirection = LayoutDirection.Vertical;
            ui.Constraints = new Constraints
            {
                Anchors = Anchors.Center,
                Width = new Fixed(300),
            };
            
            bool controlHints = ui.Checkbox(id: "Checkbox_ControlHints", text: _localization.GetString("ui.setting.controlHints")!, isChecked: _gameplaySettings.ControlHints, _soundEffectService);
            _gameplaySettings.ControlHints.Set(controlHints);
            
            bool crosshair = ui.Checkbox(id: "Checkbox_Crosshair", text: _localization.GetString("ui.setting.crosshair")!, isChecked: _gameplaySettings.Crosshair, _soundEffectService);
            _gameplaySettings.Crosshair.Set(crosshair);
            
            bool autosave = ui.Checkbox(id: "Checkbox_Autosave", text: _localization.GetString("ui.setting.autosave")!, isChecked: _gameplaySettings.Autosave, _soundEffectService);
            _gameplaySettings.Autosave.Set(autosave);

            if (autosave)
            {
                ui.NumberControl(
                    id: "Control_AutosaveMinutes",
                    text: _localization.GetString("ui.setting.autosave.interval.minutes")!,
                    _gameplaySettings.AutosaveIntervalMs,
                    constraints: new Int2(1000 * 60, 1000 * 60 * 60), //  1 minute to 1 hour
                    display: new Int2(1, 60),
                    steps: 59,
                    _soundEffectService,
                    OnAutosaveIntervalChanged
                );
            }
            
            ui.NumberControl(
                id: "Control_FOV",
                text: _localization.GetString("ui.setting.fov")!,
                _renderSettings.FOV,
                constraints: new Int2(60, 120),
                display: new Int2(60, 120),
                steps: 12,
                _soundEffectService,
                OnFOVChanged
            );
        }
        
        using (ui.Element())
        {
            ui.Constraints = new Constraints
            {
                Width = new Fill(),
                Height = new Fill(),
            };
        }
        
        using (ui.Element())
        {
            ui.Constraints = new Constraints
            {
                Anchors = Anchors.Center,
            };
        
            using (ui.Text(_localization.GetString("ui.menu.volume")!))
            {
                ui.FontSize = 24;
            }
        }
        
        using (ui.Element())
        {
            ui.LayoutDirection = LayoutDirection.Vertical;
            ui.Constraints = new Constraints
            {
                Anchors = Anchors.Center,
                Width = new Fixed(300),
            };
            
            ui.NumberControl(
                id: "Control_Volume_Master",
                text: _localization.GetString("ui.setting.volume.master")!,
                _volumeSettings.Master,
                constraints: new Float2(0f, 1f),
                display: new Int2(0, 100),
                steps: 20,
                _soundEffectService,
                OnMasterVolumeChanged
            );
        
            ui.NumberControl(
                id: "Control_Volume_Interface",
                text: _localization.GetString("ui.setting.volume.interface")!,
                _volumeSettings.Interface,
                constraints: new Float2(0f, 1f),
                display: new Int2(0, 100),
                steps: 20,
                _soundEffectService,
                OnInterfaceVolumeChanged
            );
        
            ui.NumberControl(
                id: "Control_Volume_Effects",
                text: _localization.GetString("ui.setting.volume.effects")!,
                _volumeSettings.Effects,
                constraints: new Float2(0f, 1f),
                display: new Int2(0, 100),
                steps: 20,
                _soundEffectService,
                OnEffectsVolumeChanged
            );
            
            ui.NumberControl(
                id: "Control_Volume_Music",
                text: _localization.GetString("ui.setting.volume.music")!,
                _volumeSettings.Music,
                constraints: new Float2(0f, 1f),
                display: new Int2(0, 100),
                steps: 20,
                _soundEffectService,
                OnMusicVolumeChanged
            );
        }
        
        using (ui.Element())
        {
            ui.Constraints = new Constraints
            {
                Width = new Fill(),
                Height = new Fill(),
            };
        }
        
        using (ui.Element())
        {
            ui.Constraints = new Constraints
            {
                Anchors = Anchors.Center,
            };

            using (ui.Text(_localization.GetString("ui.menu.display")!))
            {
                ui.FontSize = 24;
            }
        }

        using (ui.Element())
        {
            ui.LayoutDirection = LayoutDirection.Vertical;
            ui.Constraints = new Constraints
            {
                Anchors = Anchors.Center,
                Width = new Fixed(300),
            };

            bool value = ui.Checkbox(id: "Checkbox_VSync", text: _localization.GetString("ui.setting.vsync")!, isChecked: _renderSettings.VSync, _soundEffectService);
            _renderSettings.VSync.Set(value);

            value = ui.Checkbox(id: "Checkbox_Fullscreen", text: _localization.GetString("ui.setting.fullscreen")!, isChecked: _windowSettings.Mode == WindowMode.Fullscreen, _soundEffectService);
            _windowSettings.Mode.Set(value ? WindowMode.Fullscreen : WindowMode.Maximized);

            value = ui.Checkbox(id: "Checkbox_Borderless", text: _localization.GetString("ui.setting.borderless")!, isChecked: _windowSettings.Borderless, _soundEffectService);
            _windowSettings.Borderless.Set(value);

            value = ui.Checkbox(id: "Checkbox_MSAA", text: _localization.GetString("ui.setting.msaa")!, isChecked: _renderSettings.AntiAliasing == AntiAliasing.MSAA, _soundEffectService);
            _renderSettings.AntiAliasing.Set(value ? AntiAliasing.MSAA : AntiAliasing.None);
            
            value = ui.Checkbox(id: "Checkbox_SAO", text: _localization.GetString("ui.setting.sao")!, isChecked: _renderSettings.SAO.Enabled, _soundEffectService);
            _renderSettings.SAO.Enabled.Set(value);
            
            value = ui.Checkbox(id: "Checkbox_Bloom", text: _localization.GetString("ui.setting.bloom")!, isChecked: _renderSettings.Bloom.Enabled, _soundEffectService);
            _renderSettings.Bloom.Enabled.Set(value);
            
            ui.NumberControl(
                id: "Control_RenderDistance",
                text: _localization.GetString("ui.setting.renderDistance")!,
                _renderSettings.FarPlane,
                constraints: new Float2(500, 5_000),
                display: new Int2(1, 10),
                steps: 9,
                _soundEffectService,
                OnRenderDistanceChanged
            );
        }

        using (ui.Element())
        {
            ui.Constraints = new Constraints
            {
                Width = new Fill(),
                Height = new Fill(),
            };
        }
        
        using (ui.TextButton(id: "Button_Back", text: _localization.GetString("ui.button.back")!, _buttonOptions, out Widgets.Interactions interactions))
        {
            ui.Constraints = new Constraints
            {
                Anchors = Anchors.Center,
            };

            if (interactions.Has(Widgets.Interactions.Click))
            {
                _settingsManager.ApplySettings();
                menu.GoBack();
            }
        }
        
        return Result.FromSuccess();
    }

    private void OnFOVChanged(int oldValue, int newValue, int change)
    {
        _renderSettings.FOV.Set(newValue);
    }
    
    private void OnLookSensitivityChanged(int oldValue, int newValue, int change)
    {
        _controlSettings.LookSensitivity.Set(newValue);
    }

    private void OnMasterVolumeChanged(float oldValue, float newValue, float change)
    {
        _volumeSettings.Master.Set(newValue);
    }

    private void OnInterfaceVolumeChanged(float oldValue, float newValue, float change)
    {
        _volumeSettings.Interface.Set(newValue);
    }
    
    private void OnEffectsVolumeChanged(float oldValue, float newValue, float change)
    {
        _volumeSettings.Effects.Set(newValue);
    }
    
    private void OnMusicVolumeChanged(float oldValue, float newValue, float change)
    {
        _volumeSettings.Music.Set(newValue);
    }
    
    private void OnAutosaveIntervalChanged(int oldValue, int newValue, int change)
    {
        _gameplaySettings.AutosaveIntervalMs.Set(newValue);
    }
    
    private void OnRenderDistanceChanged(float oldValue, float newValue, float change)
    {
        _renderSettings.FarPlane.Set(newValue);
    }
}