using System;
using System.Collections.Generic;
using System.Globalization;
using Reef;
using Reef.UI;
using Swordfish.Graphics;
using Swordfish.Library.Collections;
using Swordfish.Library.Util;
using WaywardBeyond.Data.Localization;

namespace WaywardBeyond.Client.UI.Layers;

/// <summary>Draws the full-screen loading state.</summary>
internal sealed class LoadScreen(
    in IAssetDatabase<LocalizedTags> localizedTagDatabase
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
    private const int SIGNAL_TRAVEL = SIGNAL_WIDTH + SIGNAL_SEGMENT_WIDTH * 2;
    private const float SIGNAL_PERIOD_SECONDS = 2f;

    private readonly IAssetDatabase<LocalizedTags> _localizedTagDatabase = localizedTagDatabase;
    private readonly Randomizer _randomizer = new();

    private double _currentTime;
    private bool _wasVisible;

    private string _status = string.Empty;
    private string _lastStatus = string.Empty;
    private double _statusStartTime;
    private double _statusEndTime;

    private string _hint = string.Empty;
    private string _lastHint = string.Empty;
    private double _hintStartTime;
    private double _hintEndTime;

    public bool IsVisible()
    {
        bool visible = WaywardBeyond.GameState == GameState.Loading;
        if (visible && !_wasVisible)
        {
            _currentTime = 0;
            _status = string.Empty;
            _lastStatus = string.Empty;
            _statusStartTime = 0;
            _statusEndTime = 0;
            _hint = string.Empty;
            _lastHint = string.Empty;
            _hintStartTime = 0;
            _hintEndTime = HINT_START_DELAY_SECONDS;
        }

        _wasVisible = visible;
        return visible;
    }

    public Result RenderUI(double delta, UIBuilder<Material> ui)
    {
        _currentTime += delta;
        UpdateCopy();

        float contentFade = Math.Clamp((float)(_currentTime / FADE_IN_SECONDS), 0f, 1f);
        float statusFade = Math.Clamp(
            (float)((_currentTime - _statusStartTime) / FADE_IN_SECONDS),
            0f,
            1f
        );
        float hintFade = Math.Clamp(
            (float)((_currentTime - _hintStartTime) / FADE_IN_SECONDS),
            0f,
            1f
        );

        float progress = (float)(_currentTime / SIGNAL_PERIOD_SECONDS % 1d);
        int segmentX = (int)(progress * SIGNAL_TRAVEL) - SIGNAL_SEGMENT_WIDTH;

        new LoadScreenView().Render(
            ui,
            new LoadScreenState(_status, _hint, contentFade, statusFade, hintFade, segmentX)
        );

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
            _statusStartTime = _currentTime;
            _statusEndTime = _currentTime + STATUS_DWELL_SECONDS + CountWords(_status) * SECONDS_PER_WORD;
        }

        if (_currentTime >= _hintEndTime)
        {
            Result<LocalizedTags> localizedTags = _localizedTagDatabase.Get(CultureInfo.CurrentCulture.TwoLetterISOLanguageName);
            IReadOnlyList<string>? tags = localizedTags.Success ? localizedTags.Value.GetValues("game_hints") : null;
            _hint = tags != null ? SelectDifferent(tags, _lastHint) : string.Empty;
            _lastHint = _hint;
            _hintStartTime = _currentTime;
            _hintEndTime = _currentTime + HINT_DWELL_SECONDS + CountWords(_hint) * SECONDS_PER_WORD;
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
