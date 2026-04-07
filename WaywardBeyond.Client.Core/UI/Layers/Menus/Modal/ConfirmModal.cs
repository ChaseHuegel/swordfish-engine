using System;
using System.Collections.Concurrent;
using System.Numerics;
using Microsoft.Extensions.Logging;
using Reef;
using Reef.UI;
using Swordfish.Graphics;
using Swordfish.Library.Globalization;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Core.Services;

namespace WaywardBeyond.Client.Core.UI.Layers.Menus.Modal;

internal class ConfirmModal(in ILogger<ConfirmModal> logger, in SoundEffectService soundEffectService, in ILocalization localization)
    : IMenuPage<Modal>
{
    private static Modal Modal { get; } = new(id: "core.confirm");

    private readonly ILogger<ConfirmModal> _logger = logger;
    private readonly ILocalization _localization = localization;
    
    private readonly ConcurrentStack<Action> _actions = new();

    private readonly Widgets.ButtonOptions _menuButtonOptions = new(
        new FontOptions {
            Size = 32,
        },
        new Widgets.AudioOptions(soundEffectService)
    );

    public Modal ID => Modal;

    public Result RenderPage(double delta, UIBuilder<Material> ui, Menu<Modal> menu)
    {
        ui.Constraints = new Constraints
        {
            Anchors = Anchors.Center,
        };
        
        using (ui.Element())
        {
            ui.Color = new Vector4(0f, 0f, 0f, 0.75f);
            
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
                    ui.FontSize = 24;
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
                    using (ui.TextButton(id: "Button_No", text: _localization.GetString("ui.button.no")!, _menuButtonOptions, out Widgets.Interactions interactions))
                    {
                        ui.Constraints = new Constraints
                        {
                            Anchors = Anchors.Center,
                        };

                        if (interactions.Has(Widgets.Interactions.Click))
                        {
                            _actions.TryPop(out _);
                            menu.GoBack();
                        }
                    }
                }

                using (ui.Element())
                {
                    using (ui.TextButton(id: "Button_Yes", text: _localization.GetString("ui.button.yes")!, _menuButtonOptions, out Widgets.Interactions interactions))
                    {
                        ui.Constraints = new Constraints
                        {
                            Anchors = Anchors.Center,
                        };

                        if (interactions.Has(Widgets.Interactions.Click))
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
    }
    
    public Modal Create(Action action)
    {
        _actions.Push(action);
        return new Modal(id: "core.confirm");
    }
}