using Shoal.DependencyInjection;
using Swordfish.Graphics;
using Swordfish.Settings;
using WaywardBeyond.Config;

namespace WaywardBeyond.Client.Configuration;

internal sealed class ConfigManager : IAutoActivate
{
    private readonly ControlSettings _controlSettings;
    private readonly WindowSettings _windowSettings;
    private readonly RenderSettings _renderSettings;
    private readonly AudioSettings _audioSettings;
    private readonly VolumeSettings _volumeSettings;
    private readonly GameplayConfig _gameplayConfig;
    private readonly NetworkingConfig _networkingConfig;
    private readonly UISettings _uiSettings;
    private readonly LevelGeneratorConfig _levelGeneratorConfig;
    private readonly ChatConfig _chatConfig;
    private readonly ChatConfig _storageConfig;

    public ConfigManager(
        in IWindowContext windowContext,
        in ControlSettings controlSettings,
        in WindowSettings windowSettings,
        in RenderSettings renderSettings,
        in AudioSettings audioSettings,
        in VolumeSettings volumeSettings,
        in GameplayConfig gameplayConfig,
        in NetworkingConfig networkingConfig,
        in UISettings uiSettings,
        in LevelGeneratorConfig levelGeneratorConfig,
        in ChatConfig chatConfig,
        in ChatConfig storageConfig
    ) {
        _controlSettings = controlSettings;
        _windowSettings = windowSettings;
        _renderSettings = renderSettings;
        _audioSettings = audioSettings;
        _volumeSettings = volumeSettings;
        _gameplayConfig = gameplayConfig;
        _networkingConfig = networkingConfig;
        _uiSettings = uiSettings;
        _levelGeneratorConfig = levelGeneratorConfig;
        _chatConfig = chatConfig;
        _storageConfig = storageConfig;

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
        _levelGeneratorConfig.Save();
        _chatConfig.Save();
        _storageConfig.Save();
    }

    private void OnWindowClosed()
    {
        ApplySettings();
    }
}