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
    private readonly GameplayConfig _gameplayConfig;
    private readonly NetworkingConfig _networkingConfig;
    private readonly UISettings _uiSettings;

    public SettingsManager(
        in IWindowContext windowContext,
        in ControlSettings controlSettings,
        in WindowSettings windowSettings,
        in RenderSettings renderSettings,
        in AudioSettings audioSettings,
        in VolumeSettings volumeSettings,
        in GameplayConfig gameplayConfig,
        in NetworkingConfig networkingConfig,
        in UISettings uiSettings
    ) {
        _controlSettings = controlSettings;
        _windowSettings = windowSettings;
        _renderSettings = renderSettings;
        _audioSettings = audioSettings;
        _volumeSettings = volumeSettings;
        _gameplayConfig = gameplayConfig;
        _networkingConfig = networkingConfig;
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
        _gameplayConfig.Save();
        _networkingConfig.Save();
        _uiSettings.Save();
    }

    private void OnWindowClosed()
    {
        ApplySettings();
    }
}