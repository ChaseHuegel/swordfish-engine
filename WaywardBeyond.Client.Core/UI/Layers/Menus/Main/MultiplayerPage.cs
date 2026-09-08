using System;
using System.Threading.Tasks;
using System.Numerics;
using Reef;
using Reef.Constraints;
using Reef.UI;
using Swordfish.Graphics;
using Swordfish.Library.Globalization;
using Swordfish.Library.IO;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Core.Networking;
using WaywardBeyond.Client.Core.Saves;
using WaywardBeyond.Client.Core.Services;
using WaywardBeyond.Shared.Config;

namespace WaywardBeyond.Client.Core.UI.Layers.Menus.Main;

internal sealed class MultiplayerPage : IMenuPage<MenuPage>
{
    public MenuPage ID => MenuPage.Multiplayer;

    private readonly TransportManager _transportManager;
    private readonly GameSaveService _gameSaveService;
    private readonly NetworkingSettings _networkingSettings;
    private readonly IInputService _inputService;
    private readonly SoundEffectService _soundEffectService;
    private readonly ILocalization _localization;

    private readonly Widgets.ButtonOptions _menuButtonOptions;
    private readonly Widgets.ButtonOptions _buttonOptions;

    private TextBoxState _hostTextBox;
    private TextBoxState _portTextBox;
    private string? _errorMessage;

    public MultiplayerPage(
        in TransportManager transportManager,
        in GameSaveService gameSaveService,
        in NetworkingSettings networkingSettings,
        in IInputService inputService,
        in SoundEffectService soundEffectService,
        in ILocalization localization
    ) {
        _transportManager = transportManager;
        _gameSaveService = gameSaveService;
        _networkingSettings = networkingSettings;
        _inputService = inputService;
        _soundEffectService = soundEffectService;
        _localization = localization;

        _menuButtonOptions = new Widgets.ButtonOptions(
            new FontOptions { Size = 32, },
            new Widgets.AudioOptions(soundEffectService)
        );

        _buttonOptions = new Widgets.ButtonOptions(
            new FontOptions { Size = 20, },
            new Widgets.AudioOptions(soundEffectService)
        );

        _hostTextBox = new TextBoxState(
            initialValue: string.Empty,
            new TextBoxState.Options(
                Placeholder: localization.GetString("ui.field.host"),
                MaxCharacters: 253,
                Constraints: new Constraints { Width = new Fixed(300), }
            )
        );

        int defaultPort = networkingSettings.DefaultConnectPort.Get();
        _portTextBox = new TextBoxState(
            initialValue: defaultPort.ToString(),
            new TextBoxState.Options(
                Placeholder: localization.GetString("ui.field.port"),
                MaxCharacters: 5,
                DisallowedCharacters: [' ', '\t', '\n', '\r'],
                Constraints: new Constraints { Width = new Fixed(300), }
            )
        );
    }

    public Result RenderPage(double delta, UIBuilder<Material> ui, Menu<MenuPage> menu)
    {
        using (ui.Element())
        {
            ui.Constraints = new Constraints { Anchors = Anchors.Center, };

            using (ui.Text(_localization.GetString("ui.menu.multiplayer")!))
            {
                ui.FontSize = 24;
            }
        }

        using (ui.Element())
        {
            ui.Spacing = 8;
            ui.LayoutDirection = LayoutDirection.Vertical;
            ui.Constraints = new Constraints { Anchors = Anchors.Center, };

            ui.TextBox(id: "TextBox_Host", state: ref _hostTextBox, _buttonOptions.FontOptions, _inputService, _soundEffectService);

            ui.TextBox(id: "TextBox_Port", state: ref _portTextBox, _buttonOptions.FontOptions, _inputService, _soundEffectService);

            if (_errorMessage != null)
            {
                using (ui.Text(_errorMessage))
                {
                    ui.Color = new Vector4(1f, 0f, 0f, 1f);
                }
            }

            using (ui.TextButton(id: "Button_Connect", text: _localization.GetString("ui.button.connect")!, _buttonOptions, out Widgets.Interactions interactions))
            {
                ui.Constraints = new Constraints { Anchors = Anchors.Center, };

                if (interactions.Has(Widgets.Interactions.Click))
                {
                    TryConnect(menu);
                }
            }
        }

        using (ui.Element())
        {
            ui.Constraints = new Constraints { Width = new Fill(), Height = new Fill(), };
        }

        using (ui.Element())
        {
            ui.Constraints = new Constraints { Anchors = Anchors.Center, };

            using (ui.TextButton(id: "Button_Back", text: _localization.GetString("ui.button.back")!, _menuButtonOptions, out Widgets.Interactions interactions))
            {
                ui.Constraints = new Constraints { Anchors = Anchors.Center, };

                if (interactions.Has(Widgets.Interactions.Click))
                {
                    menu.GoBack();
                }
            }
        }

        return Result.FromSuccess();
    }

    private void TryConnect(Menu<MenuPage> menu)
    {
        string host = _hostTextBox.Text.ToString().Trim();
        string portText = _portTextBox.Text.ToString().Trim();

        if (string.IsNullOrWhiteSpace(host))
        {
            _errorMessage = _localization.GetString("ui.notification.connect.hostRequired");
            return;
        }

        if (!int.TryParse(portText, out int port) || port < 1 || port > 65535)
        {
            _errorMessage = _localization.GetString("ui.notification.connect.portInvalid");
            return;
        }

        Result result = _transportManager.ConnectRemote(host, port);
        if (!result.Success)
        {
            _errorMessage = _localization.GetString("ui.notification.connect.failed");
            return;
        }

        _errorMessage = null;
        _ = _gameSaveService.RefreshWorldsAsync();
        menu.GoToPage(MenuPage.SelectSave);
    }
}