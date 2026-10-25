using System;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Reef;
using Reef.Constraints;
using Reef.UI;
using Swordfish.Graphics;
using Swordfish.Library.Globalization;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Services;
using WaywardBeyond.Client.UI.Theme;

namespace WaywardBeyond.Client.UI.Layers.Menus.Modal;

internal class ConfirmModal(in ILogger<ConfirmModal> logger, in SoundEffectService soundEffectService, in ILocalization localization)
    : IMenuPage<Modal>
{
    private static Modal Modal { get; } = new(id: "core.confirm");

    private readonly ILogger<ConfirmModal> _logger = logger;
    private readonly ILocalization _localization = localization;

    private readonly ConcurrentStack<Action> _actions = new();

    private readonly Widgets.ActionButtonOptions _secondaryButton = Widgets.ActionButtonOptions.Menu(
        ButtonVariant.Secondary,
        soundEffectService
    );
    private readonly Widgets.ActionButtonOptions _primaryButton = Widgets.ActionButtonOptions.Menu(
        ButtonVariant.Danger,
        soundEffectService
    );

    public Modal ID => Modal;

    public Result RenderPage(double delta, UIBuilder<Material> ui, Menu<Modal> menu)
    {
        const int panelWidth = 520;
        const int panelHeight = 250;

        using (ui.Element())
        {
            ui.Color = UiTheme.Scrim;
            ui.Constraints = new Constraints
            {
                Width = new Fill(),
                Height = new Fill(),
            };

            using (ui.Element())
            {
                ui.LayoutDirection = LayoutDirection.None;
                ui.Constraints = new Constraints
                {
                    Anchors = Anchors.Center,
                    Width = new Fixed(panelWidth),
                    Height = new Fixed(panelHeight),
                };
                ui.ClipConstraints = new Constraints
                {
                    Width = new Fixed(panelWidth),
                    Height = new Fixed(panelHeight),
                };

                using (ui.MenuPanel(Widgets.Rect(0, 0, panelWidth, panelHeight)))
                {
                    int padding = 28;
                    int contentWidth = panelWidth - padding * 2;

                    ui.Label(
                        _localization.GetString("ui.menu.confirm")!,
                        Widgets.Rect(padding, 22, contentWidth, 30),
                        UiMetrics.MenuTitleFont,
                        UiTheme.Text,
                        Anchors.Center
                    );
                    ui.Divider(
                        Widgets.Rect(padding, 62, contentWidth, 1),
                        UiTheme.Border
                    );
                    ui.Paragraph(
                        _localization.GetString("ui.menu.confirmBody")!,
                        Widgets.Rect(padding, 86, contentWidth, 56),
                        UiMetrics.MenuBodyFont,
                        UiTheme.TextMuted
                    );

                    int buttonY = panelHeight - 68;
                    if (ui.ActionButton(
                            "Button_No",
                            _localization.GetString("ui.button.no")!,
                            _secondaryButton with
                            {
                                Rect = Widgets.Rect(padding, buttonY, 180, UiMetrics.IconButtonSize),
                            }
                        ))
                    {
                        _actions.TryPop(out _);
                        menu.GoBack();
                    }

                    if (ui.ActionButton(
                            "Button_Yes",
                            _localization.GetString("ui.button.yes")!,
                            _primaryButton with
                            {
                                Rect = Widgets.Rect(panelWidth - padding - 180, buttonY, 180, UiMetrics.IconButtonSize),
                            }
                        ))
                    {
                        if (_actions.TryPop(out Action? action))
                        {
                            try
                            {
                                action();
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Error raising a confirm modal action.");
                            }
                        }

                        menu.GoBack();
                    }
                }
            }
        }

        return Result.FromSuccess();
    }

    public Modal Create(Action action)
    {
        _actions.Push(action);
        return new Modal(id: "core.confirm");
    }
}
