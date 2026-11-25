using Avalonia;
using Avalonia.Controls;

namespace WaywardBeyond.AssetStudio.Controls;

/// <summary>
///     Unified scalar form row pairing a friendly control with a raw TOML token toggle.
/// </summary>
public sealed partial class ScalarFieldRowControl : UserControl
{
    /// <summary>Identifies the <see cref="CompactStacked"/> styled property.</summary>
    public static readonly StyledProperty<bool> CompactStackedProperty =
        AvaloniaProperty.Register<ScalarFieldRowControl, bool>(nameof(CompactStacked));

    /// <summary>
    ///     Initializes the scalar field row control.
    /// </summary>
    public ScalarFieldRowControl()
    {
        InitializeComponent();
        ApplyLayoutMode();
    }

    /// <summary>Stacks the editor full width below the label header for narrow inspector sidebars.</summary>
    public bool CompactStacked
    {
        get => GetValue(CompactStackedProperty);
        set => SetValue(CompactStackedProperty, value);
    }

    /// <inheritdoc/>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == CompactStackedProperty)
        {
            ApplyLayoutMode();
        }
    }

    private void ApplyLayoutMode()
    {
        if (CompactStacked)
        {
            PART_RootBorder.Padding = new Thickness(0, 3);
            PART_FieldGrid.ColumnDefinitions = ColumnDefinitions.Parse("*,30");
            PART_FieldGrid.RowSpacing = 4;
            Grid.SetRow(PART_LabelPanel, 0);
            Grid.SetColumn(PART_LabelPanel, 0);
            PART_LabelPanel.Margin = new Thickness(0, 0, 6, 0);
            Grid.SetRow(PART_RawButton, 0);
            Grid.SetColumn(PART_RawButton, 1);
            PART_RawButton.Margin = new Thickness(4, 0, 0, 0);
            Grid.SetRow(PART_EditorHost, 1);
            Grid.SetColumn(PART_EditorHost, 0);
            Grid.SetColumnSpan(PART_EditorHost, 2);
            PART_DangerBanner.Margin = new Thickness(0);
        }
        else
        {
            PART_RootBorder.Padding = new Thickness(6, 3);
            PART_FieldGrid.ColumnDefinitions = ColumnDefinitions.Parse("192,*,30");
            PART_FieldGrid.RowSpacing = 0;
            Grid.SetRow(PART_LabelPanel, 0);
            Grid.SetColumn(PART_LabelPanel, 0);
            PART_LabelPanel.Margin = new Thickness(0, 5, 10, 0);
            Grid.SetRow(PART_EditorHost, 0);
            Grid.SetColumn(PART_EditorHost, 1);
            Grid.SetColumnSpan(PART_EditorHost, 1);
            Grid.SetRow(PART_RawButton, 0);
            Grid.SetColumn(PART_RawButton, 2);
            PART_RawButton.Margin = new Thickness(4, 1, 0, 0);
            PART_DangerBanner.Margin = new Thickness(192, 0, 30, 0);
        }
    }
}
