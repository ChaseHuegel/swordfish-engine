using System.Numerics;
using System.Threading.Tasks;
using Reef;
using Reef.Constraints;
using Reef.UI;
using Swordfish.Audio;
using Swordfish.Graphics;
using Swordfish.Library.Globalization;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Core.Configuration;
using WaywardBeyond.Client.Core.Services;

namespace WaywardBeyond.Client.Core.UI.Layers.Menus.Modal;

internal class PlaytestNoticeModal(
    in IAudioService audioService,
    in VolumeSettings volumeSettings,
    in ILocalization localization,
    in ExternalAppService externalAppService
) : IMenuPage<Modal>
{
    public static Modal Modal { get; } = new(id: "core.playtest");
    
    private readonly ILocalization _localization = localization;
    private readonly ExternalAppService _externalAppService = externalAppService;

    private readonly Widgets.ButtonOptions _menuButtonOptions = new(
        new FontOptions {
            Size = 32,
        },
        new Widgets.AudioOptions(audioService, volumeSettings)
    );
    
    private readonly Widgets.ButtonOptions _iconButtonOptions = new(
        new FontOptions {
            ID = "Font Awesome 6 Free Brands",
            Size = 32,
        },
        new Widgets.AudioOptions(audioService, volumeSettings)
    );

    public Modal ID => Modal;

    public Result RenderPage(double delta, UIBuilder<Material> ui, Menu<Modal> menu)
    {
        using (ui.Element())
        {
            //  Fullscreen tint
            ui.Color = new Vector4(0f, 0f, 0f, 0.95f);
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
                
                using (ui.Text(_localization.GetString("ui.menu.playtest")!))
                {
                    ui.FontSize = 24;
                }
            }

            using (ui.Element())
            {
                ui.Constraints = new Constraints
                {
                    Anchors = Anchors.Center,
                    Width = new Relative(0.35f),
                    Height = new Fixed(300),
                };
                ui.ClipConstraints = new Constraints
                {
                    Width = new Relative(1f),
                    Height = new Relative(1f),
                };
                
                using (ui.Text(_localization.GetString("ui.text.playtest")!)) {}
            }
            
            using (ui.Element())
            {
                ui.Constraints = new Constraints
                {
                    Anchors = Anchors.Center,
                };
                
                using (ui.TextButton(id: "Button_Close", text: _localization.GetString("ui.button.close")!, _menuButtonOptions, out Widgets.Interactions interactions))
                {
                    ui.Constraints = new Constraints
                    {
                        Anchors = Anchors.Center,
                    };

                    if (interactions.Has(Widgets.Interactions.Click))
                    {
                        menu.GoBack();
                    }
                }
            }
            
            using (ui.Element())
            {
                ui.Constraints = new Constraints
                {
                    Anchors = Anchors.Center,
                };
                
                using (ui.TextButton(id: "Button_Community", text: "\uf392", _iconButtonOptions, out Widgets.Interactions interactions))
                {
                    ui.Padding = new Padding(8);
                    
                    if (interactions.Has(Widgets.Interactions.Click))
                    {
                        Task.Run(_externalAppService.TryOpenDiscordAsync);
                    }
                }
                
                using (ui.TextButton(id: "Button_Wishlist", text: "\uf1b6", _iconButtonOptions, out Widgets.Interactions interactions))
                {
                    ui.Padding = new Padding(8);
                    
                    if (interactions.Has(Widgets.Interactions.Click))
                    {
                        Task.Run(_externalAppService.TryOpenSteamAsync);
                    }
                }
            }

            using (ui.Element())
            {
                ui.Constraints = new Constraints
                {
                    Width = new Fill(),
                    Height = new Fill(),
                };
            }
            
            return Result.FromSuccess();
        }
    }
}