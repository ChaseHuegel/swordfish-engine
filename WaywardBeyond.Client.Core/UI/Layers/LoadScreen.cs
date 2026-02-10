using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Text;
using Reef;
using Reef.Constraints;
using Reef.UI;
using Swordfish.Graphics;
using Swordfish.Integrations;
using Swordfish.Library.Collections;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Core.Meta;
using WaywardBeyond.Client.Core.Saves;

namespace WaywardBeyond.Client.Core.UI.Layers;

internal sealed class LoadScreen(
    in GameSaveService gameSaveService,
    in IAssetDatabase<LocalizedTags> localizedTagDatabase
) : IUILayer
{
    private const float WORDS_PER_MINUTE = 150;
    private const float WORDS_PER_SECOND = WORDS_PER_MINUTE / 60f;
    private const float SECONDS_PER_WORD = 1f / WORDS_PER_SECOND;
    
    private readonly GameSaveService _gameSaveService = gameSaveService;
    private readonly IAssetDatabase<LocalizedTags> _localizedTagDatabase = localizedTagDatabase;
    private readonly Randomizer _randomizer = new();
    
    private double _currentTime;
    
    private string _hint = string.Empty;
    private double _hintStartTime;
    private double _hintEndTime;
    
    public bool IsVisible()
    {
        return WaywardBeyond.GameState == GameState.Loading;
    }

    public Result RenderUI(double delta, UIBuilder<Material> ui)
    {
        _currentTime += delta;

        if (_currentTime >= _hintEndTime)
        {
            _hintStartTime = _currentTime;
            Result<LocalizedTags> localizedTags = _localizedTagDatabase.Get(CultureInfo.CurrentCulture.TwoLetterISOLanguageName);
            IReadOnlyList<string>? tags = localizedTags.Success ? localizedTags.Value.GetValues("game_hints") : null;
        
            _hint = tags != null ? _randomizer.Select(tags) : string.Empty;
            _hintEndTime = _hintStartTime + CountWords(_hint) * SECONDS_PER_WORD;
        }

        using (ui.Element())
        {
            ui.Color = new Vector4(0f, 0f, 0f, 1f);
            ui.Constraints = new Constraints
            {
                Width = new Relative(1f),
                Height = new Relative(1f),
            };
            ui.LayoutDirection = LayoutDirection.Vertical;
            ui.Spacing = 20;

            using (ui.Element())
            {
                ui.Constraints = new Constraints
                {
                    Width = new Fill(),
                    Height = new Fill(),
                };
            }

            var statusBuilder = new StringBuilder();
            int steps = MathS.WrapInt((int)(_currentTime * 3d), 0, 3);
            for (var i = 0; i < 3; i++)
            {
                statusBuilder.Append(i == steps ? FontAwesome.CIRCLE_DOT : FontAwesome.CIRCLE);
                if (i != 2)
                {
                    statusBuilder.Append(' ');
                }
            }

            using (ui.Text(statusBuilder.ToString()))
            {
                ui.FontSize = 24;
                ui.FontID = "Font Awesome 6 Free Regular";
                ui.Constraints = new Constraints
                {
                    Anchors = Anchors.Center | Anchors.Bottom,
                };
            }
            
            using (ui.Text(_gameSaveService.GetStatus()))
            {
                ui.FontSize = 16;
                ui.Constraints = new Constraints
                {
                    Anchors = Anchors.Center | Anchors.Bottom,
                };
            }
            
            using (ui.Text(_hint))
            {
                ui.FontSize = 14;
                ui.Color = new Vector4(0.5f, 0.5f, 0.5f, 1f);
                ui.Constraints = new Constraints
                {
                    Anchors = Anchors.Center | Anchors.Bottom,
                };
            }
            
            using (ui.Element())
            {
                ui.Constraints = new Constraints
                {
                    Width = new Fill(),
                    Height = new Fill(),
                };
            }
        }
        
        return Result.FromSuccess();
    }
    
    private static int CountWords(in string text)
    {
        var wordCount = 0;
        var inWord = false;

        foreach (char c in text)
        {
            if (char.IsWhiteSpace(c) || c == '-' || c == '—' || c == '–')
            {
                inWord = false;
                continue;
            }
            
            if (!inWord)
            {
                wordCount++;
            }
            
            inWord = true;
        }

        return wordCount;
    }
}