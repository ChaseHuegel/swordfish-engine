using Microsoft.Extensions.Logging;
using Reef;
using Reef.Constraints;
using Reef.UI;
using Swordfish.Graphics;
using Swordfish.Library.Collections;
using Swordfish.Library.Util;
using Swordfish.UI.Reef;

namespace WaywardBeyond.Client.Core.UI.Layers.Menus;

internal abstract class TitleMenu<TIdentifier> : Menu<TIdentifier>
    where TIdentifier : notnull
{
    private readonly Material? _titleMaterial;

    protected TitleMenu(
        ILogger<Menu<TIdentifier>> logger,
        IAssetDatabase<Material> materialDatabase,
        ReefContext reefContext,
        IMenuPage<TIdentifier>[] pages
    ) : base(logger, reefContext, pages)
    {
        Result<Material> materialResult = materialDatabase.Get("ui/menu/title");
        if (materialResult)
        {
            _titleMaterial = materialResult;
        }
        else
        {
            logger.LogError(materialResult, "Failed to load the title material, it will not be able to render.");
        }
    }

    public override Result RenderUI(double delta, UIBuilder<Material> ui)
    {
        using (ui.Element())
        {
            ui.LayoutDirection = LayoutDirection.Vertical;
            ui.Padding = new Padding
            {
                Left = 20,
                Top = 20,
                Right = 20,
                Bottom = 20,
            };
            ui.Constraints = new Constraints
            {
                Width = new Relative(1f),
                Height = new Relative(1f),
            };

            if (_titleMaterial != null)
            {
                using (ui.Image(_titleMaterial))
                {
                    ui.Constraints = new Constraints
                    {
                        Anchors = Anchors.Center,
                        Width = new Fixed(_titleMaterial.Textures[0].Width),
                        Height = new Fixed(_titleMaterial.Textures[0].Height),
                    };
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

            return base.RenderUI(delta, ui);
        }
    }
}