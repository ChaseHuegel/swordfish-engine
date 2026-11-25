using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;

namespace WaywardBeyond.AssetStudio.Controls;

/// <summary>
///     Reusable collapsible section card with optional badge and toggleable body visibility.
/// </summary>
public sealed class CollapsibleSectionCard : ContentControl
{
    /// <summary>
    ///     Styled property for the section header title.
    /// </summary>
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<CollapsibleSectionCard, string>(nameof(Title), string.Empty);

    /// <summary>
    ///     Styled property for optional right-aligned subtitle text.
    /// </summary>
    public static readonly StyledProperty<string?> SubtitleProperty =
        AvaloniaProperty.Register<CollapsibleSectionCard, string?>(nameof(Subtitle));

    /// <summary>
    ///     Styled property for optional informational or status badge text.
    /// </summary>
    public static readonly StyledProperty<string?> BadgeTextProperty =
        AvaloniaProperty.Register<CollapsibleSectionCard, string?>(nameof(BadgeText));

    /// <summary>
    ///     Styled property tracking whether the section body is expanded.
    /// </summary>
    public static readonly StyledProperty<bool> IsExpandedProperty =
        AvaloniaProperty.Register<CollapsibleSectionCard, bool>(nameof(IsExpanded), true);

    /// <summary>
    ///     Styled property for an optional trailing action element displayed in the section header.
    /// </summary>
    public static readonly StyledProperty<object?> HeaderActionProperty =
        AvaloniaProperty.Register<CollapsibleSectionCard, object?>(nameof(HeaderAction));

    /// <summary>
    ///     Direct property indicating whether a badge is configured.
    /// </summary>
    public static readonly DirectProperty<CollapsibleSectionCard, bool> HasBadgeProperty =
        AvaloniaProperty.RegisterDirect<CollapsibleSectionCard, bool>(nameof(HasBadge), c => c.HasBadge);

    /// <summary>
    ///     Direct property indicating whether a header action element is present.
    /// </summary>
    public static readonly DirectProperty<CollapsibleSectionCard, bool> HasHeaderActionProperty =
        AvaloniaProperty.RegisterDirect<CollapsibleSectionCard, bool>(nameof(HasHeaderAction), c => c.HasHeaderAction);

    private Button? _headerButton;

    /// <summary>
    ///     Header title displayed for this collapsible section.
    /// </summary>
    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>
    ///     Optional secondary hint on the right side of the header.
    /// </summary>
    public string? Subtitle
    {
        get => GetValue(SubtitleProperty);
        set => SetValue(SubtitleProperty, value);
    }

    /// <summary>
    ///     Optional badge pill text shown beside the title.
    /// </summary>
    public string? BadgeText
    {
        get => GetValue(BadgeTextProperty);
        set => SetValue(BadgeTextProperty, value);
    }

    /// <summary>
    ///     Gets or sets whether the section body is visible.
    /// </summary>
    public bool IsExpanded
    {
        get => GetValue(IsExpandedProperty);
        set => SetValue(IsExpandedProperty, value);
    }

    /// <summary>
    ///     Optional trailing control rendered on the right edge of the header outside the collapse trigger button.
    /// </summary>
    public object? HeaderAction
    {
        get => GetValue(HeaderActionProperty);
        set => SetValue(HeaderActionProperty, value);
    }

    /// <summary>
    ///     Whether the badge pill should be visible.
    /// </summary>
    public bool HasBadge => !string.IsNullOrWhiteSpace(BadgeText);

    /// <summary>
    ///     Whether a trailing header action element is configured.
    /// </summary>
    public bool HasHeaderAction => HeaderAction != null;

    /// <inheritdoc/>
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_headerButton != null)
        {
            _headerButton.Click -= OnHeaderClicked;
        }

        _headerButton = e.NameScope.Find<Button>("PART_HeaderButton");
        if (_headerButton != null)
        {
            _headerButton.Click += OnHeaderClicked;
        }
    }

    /// <inheritdoc/>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == BadgeTextProperty)
        {
            RaisePropertyChanged(HasBadgeProperty, !string.IsNullOrWhiteSpace(change.GetOldValue<string?>()), HasBadge);
        }
        else if (change.Property == HeaderActionProperty)
        {
            RaisePropertyChanged(HasHeaderActionProperty, change.GetOldValue<object?>() != null, HasHeaderAction);
        }
    }

    private void OnHeaderClicked(object? sender, RoutedEventArgs e)
    {
        IsExpanded = !IsExpanded;
    }
}
