using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Text;
using Reef;
using Reef.Constraints;
using Reef.UI;
using Swordfish.Graphics;
using Swordfish.Library.Collections;
using Swordfish.Library.Util;
using WaywardBeyond.Client.UI.Theme;
using WaywardBeyond.Data.Localization;

namespace WaywardBeyond.Client.UI.Layers;

/// <summary>Draws the full-screen loading state.</summary>
internal sealed class LoadScreen(
    in IAssetDatabase<LocalizedTags> localizedTagDatabase,
    in IAssetDatabase<Material> materialDatabase
) : IUILayer
{
    private const float WORDS_PER_MINUTE = 150;
    private const float SECONDS_PER_WORD = 60f / WORDS_PER_MINUTE;
    private const float STATUS_DWELL_SECONDS = 2f;
    private const float HINT_DWELL_SECONDS = 4f;
    private const float HINT_START_DELAY_SECONDS = 1f;
    private const float FADE_IN_SECONDS = 0.3f;
    private const int CONTENT_WIDTH = 760;
    private const int CONTENT_HEIGHT = 74;
    private const int SIGNAL_WIDTH = 160;
    private const int SIGNAL_HEIGHT = 1;
    private const int SIGNAL_SEGMENT_WIDTH = 48;
    private const float SIGNAL_SPEED = 1f;

    private readonly IAssetDatabase<LocalizedTags> _localizedTagDatabase = localizedTagDatabase;
    private readonly Randomizer _randomizer = new();

    private double _currentTime;
    private bool _wasVisible;

    private string _status = string.Empty;
    private string _lastStatus = string.Empty;
    private double _statusEndTime;

    private string _hint = string.Empty;
    private string _lastHint = string.Empty;
    private double _hintEndTime;

    public bool IsVisible()
    {
        bool visible = WaywardBeyond.GameState == GameState.Loading;
        if (visible && !_wasVisible)
        {
            _currentTime = 0;
            _status = string.Empty;
            _lastStatus = string.Empty;
            _statusEndTime = 0;
            _hint = string.Empty;
            _lastHint = string.Empty;
            _hintEndTime = HINT_START_DELAY_SECONDS;
        }

        _wasVisible = visible;
        return visible;
    }

    public Result RenderUI(double delta, UIBuilder<Material> ui)
    {
        _currentTime += delta;
        UpdateCopy();
        
        float fade = Math.Clamp((float)(_currentTime / FADE_IN_SECONDS), 0f, 1f);
        
        using (ui.Element())
        {
            ui.LayoutDirection = LayoutDirection.None;
            ui.Constraints = new Constraints
            {
                Width = new Relative(1f),
                Height = new Relative(1f),
            };
            
            using (ui.Element())
            {
                ui.Color = UiTheme.ScrimHeavy;
                ui.Constraints = new Constraints
                {
                    Width = new Fill(),
                    Height = new Fill(),
                };
            }

            using (ui.Element())
            {
                ui.LayoutDirection = LayoutDirection.None;
                ui.Constraints = new Constraints
                {
                    Anchors = Anchors.Center,
                    Width = new Fixed(CONTENT_WIDTH),
                    Height = new Fixed(CONTENT_HEIGHT),
                };
                ui.ClipConstraints = new Constraints
                {
                    Width = new Fixed(CONTENT_WIDTH),
                    Height = new Fixed(CONTENT_HEIGHT),
                };

                ui.Label(
                    _status,
                    Widgets.Rect(0, 0, CONTENT_WIDTH, 30),
                    UiMetrics.BodyFont,
                    Fade(UiTheme.Text, fade),
                    Anchors.Center
                );
                
                DrawSignal(ui, fade);
                
                ui.Label(
                    FormatHint(_hint),
                    Widgets.Rect(0, 50, CONTENT_WIDTH, 24),
                    UiMetrics.SmallFont,
                    Fade(UiTheme.TextMuted, fade),
                    Anchors.Center
                );
            }
        }

        return Result.FromSuccess();
    }

    private void UpdateCopy()
    {
        if (_currentTime >= _statusEndTime)
        {
            Result<LocalizedTags> localizedTags = _localizedTagDatabase.Get(CultureInfo.CurrentCulture.TwoLetterISOLanguageName);
            _status = localizedTags.Success
                ? RandomFromTags(localizedTags.Value, _lastStatus, "game_load", "character_load")
                : string.Empty;
            _lastStatus = _status;
            _statusEndTime = _currentTime + STATUS_DWELL_SECONDS + CountWords(_status) * SECONDS_PER_WORD;
        }

        if (_currentTime >= _hintEndTime)
        {
            Result<LocalizedTags> localizedTags = _localizedTagDatabase.Get(CultureInfo.CurrentCulture.TwoLetterISOLanguageName);
            IReadOnlyList<string>? tags = localizedTags.Success ? localizedTags.Value.GetValues("game_hints") : null;
            _hint = tags != null ? SelectDifferent(tags, _lastHint) : string.Empty;
            _lastHint = _hint;
            _hintEndTime = _currentTime + HINT_DWELL_SECONDS + CountWords(_hint) * SECONDS_PER_WORD;
        }
    }

    private void DrawSignal(UIBuilder<Material> ui, float fade)
    {
        int lineX = (CONTENT_WIDTH - SIGNAL_WIDTH) / 2;
        using (ui.Element())
        {
            ui.LayoutDirection = LayoutDirection.None;
            ui.Color = Fade(UiTheme.Border, fade);
            ui.Constraints = Widgets.RectConstraints(
                Widgets.Rect(lineX, 38, SIGNAL_WIDTH, SIGNAL_HEIGHT)
            );

            float phase = (float)(_currentTime * 1.75f % 2d);
            float progress = phase <= 1f ? phase : 2f - phase;
            int segmentX = (int)((SIGNAL_WIDTH - SIGNAL_SEGMENT_WIDTH) * progress);

            using (ui.Element())
            {
                ui.Color = Fade(UiTheme.Accent, fade);
                ui.Constraints = new Constraints
                {
                    X = new Fixed(segmentX),
                    Y = new Fixed(0),
                    Width = new Fixed(SIGNAL_SEGMENT_WIDTH),
                    Height = new Fixed(SIGNAL_HEIGHT),
                };
            }
        }
    }

    private string RandomFromTags(LocalizedTags localizedTags, string last, params string[] tagNames)
    {
        var values = new List<string>();
        for (var i = 0; i < tagNames.Length; i++)
        {
            IReadOnlyList<string>? tags = localizedTags.GetValues(tagNames[i]);
            if (tags != null)
            {
                values.AddRange(tags);
            }
        }

        return values.Count > 0 ? SelectDifferent(values, last) : string.Empty;
    }

    private string SelectDifferent(IReadOnlyList<string> values, string last)
    {
        if (values.Count == 0)
        {
            return string.Empty;
        }

        string selected = _randomizer.Select(values);
        if (values.Count == 1 || selected != last)
        {
            return selected;
        }

        int index = 0;
        for (var i = 0; i < values.Count; i++)
        {
            if (values[i] == selected)
            {
                index = i;
                break;
            }
        }

        return values[(index + 1) % values.Count];
    }

    private static string FormatHint(string hint)
    {
        if (hint.IndexOf('[') < 0)
        {
            return hint;
        }

        var builder = new StringBuilder(hint.Length + 16);
        var inKey = false;
        for (var i = 0; i < hint.Length; i++)
        {
            char c = hint[i];
            if (c == '[' && !inKey)
            {
                builder.Append("#4BE3E3 ");
                inKey = true;
            }

            builder.Append(c);
            if (c == ']' && inKey)
            {
                builder.Append("#R ");
                inKey = false;
            }
        }

        return builder.ToString();
    }

    private static Vector4 Fade(Vector4 color, float alpha)
    {
        return new Vector4(color.X, color.Y, color.Z, color.W * alpha);
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
