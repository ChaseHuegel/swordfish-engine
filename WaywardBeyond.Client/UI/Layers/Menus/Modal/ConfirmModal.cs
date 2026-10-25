using System;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Reef;
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

    private readonly Widgets.ActionButtonOptions _secondaryButton = CompactButton(ButtonVariant.Secondary, soundEffectService);
    private readonly Widgets.ActionButtonOptions _primaryButton = CompactButton(ButtonVariant.Primary, soundEffectService);

    private static Widgets.ActionButtonOptions CompactButton(ButtonVariant variant, SoundEffectService soundEffectService)
    {
        return Widgets.ActionButtonOptions.Compact(variant, soundEffectService) with
        {
            Anchors = Anchors.Center,
        };
    }

    public Modal ID => Modal;

    public Result RenderPage(double delta, UIBuilder<Material> ui, Menu<Modal> menu)
    {
        ui.Constraints = new Constraints
        {
            Anchors = Anchors.Center,
        };

        using (ui.Element())
        {
            ui.Color = UiTheme.Scrim;

            ui.LayoutDirection = LayoutDirection.Vertical;

            ui.Padding = new Padding
            {
                Left = 20,
                Top = 20,
                Right = 20,
                Bottom = 20,
            };

            using (ui.Element())
            {
                using (ui.Text(_localization.GetString("ui.menu.confirm")!))
                {
                    ui.FontSize = UiMetrics.TitleFont;
                    ui.Color = UiTheme.Text;
                }
            }

            using (ui.Element())
            {
                ui.Spacing = 40;
                ui.Constraints = new Constraints
                {
                    Anchors = Anchors.Center,
                };

                using (ui.Element())
                {
                    if (ui.ActionButton("Button_No", _localization.GetString("ui.button.no")!, _secondaryButton))
                    {
                        _actions.TryPop(out _);
                        menu.GoBack();
                    }
                }

                using (ui.Element())
                {
                    if (ui.ActionButton("Button_Yes", _localization.GetString("ui.button.yes")!, _primaryButton))
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

            return Result.FromSuccess();
        }
    }

    public Modal Create(Action action)
    {
        _actions.Push(action);
        return new Modal(id: "core.confirm");
    }
}
