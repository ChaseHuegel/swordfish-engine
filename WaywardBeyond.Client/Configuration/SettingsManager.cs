using Shoal.DependencyInjection;
using Swordfish.Graphics;
using Swordfish.Settings;
using WaywardBeyond.Config;

namespace WaywardBeyond.Client.Configuration;

internal sealed class SettingsManager : IAutoActivate
{
    private readonly ControlSettings _controlSettings;
    private readonly WindowSettings _windowSettings;
    private readonly RenderSettings _renderSettings;
    private readonly AudioSettings _audioSettings;
    private readonly VolumeSettings _volumeSettings;
    private readonly GameplaySettings _gameplaySettings;
    private readonly NetworkingSettings _networkingSettings;
    private readonly UISettings _uiSettings;

    public SettingsManager(
        in IWindowContext windowContext,
        in ControlSettings controlSettings,
        in WindowSettings windowSettings,
        in RenderSettings renderSettings,
        in AudioSettings audioSettings,
        in VolumeSettings volumeSettings,
        in GameplaySettings gameplaySettings,
        in NetworkingSettings networkingSettings,
        in UISettings uiSettings
    ) {
        _controlSettings = controlSettings;
        _windowSettings = windowSettings;
        _renderSettings = renderSettings;
        _audioSettings = audioSettings;
        _volumeSettings = volumeSettings;
        _gameplaySettings = gameplaySettings;
        _networkingSettings = networkingSettings;
        _uiSettings = uiSettings;

        windowContext.Closed += OnWindowClosed;
        ApplySettings();
    }
    
    public void ApplySettings()
    {
        _windowSettings.Save();
        _renderSettings.Save();
        _controlSettings.Save();
        _audioSettings.Save();
        _volumeSettings.Save();
        _gameplaySettings.Save();
        _networkingSettings.Save();
        _uiSettings.Save();
    }

    private void OnWindowClosed()
    {
        ApplySettings();
    }
}