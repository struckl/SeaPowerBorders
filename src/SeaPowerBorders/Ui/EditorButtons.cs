using System;
using Noesis;

namespace SeaPowerBorders.Ui;

/// <summary>
/// Two small toggle buttons - Borders and Cities - at the top left of the mission editor's map, which has no Layers
/// menu of its own to hold the mod's entries. They sit in a grid laid over the map inside the control that holds the
/// map; the grid lets every click through, only the buttons take them. Same switches as the toggle keys.
/// </summary>
internal sealed class EditorButtons
{
    private readonly Panel host;
    private readonly Grid root;
    private readonly ToggleButton borders, cities;

    /// <summary>Puts the buttons over the map, or returns null if the map's parent is not a panel.</summary>
    public static EditorButtons TryAttach(MapControl.MapBase map, Action toggleBorders, Action toggleCities)
    {
        try
        {
            return VisualTreeHelper.GetParent(map) is Panel host ? new EditorButtons(host, toggleBorders, toggleCities) : null;
        }
        catch (Exception ex)
        {
            ModLog.Warn($"Could not add the Borders and Cities buttons to the editor map: {ex.Message}");
            return null;
        }
    }

    private EditorButtons(Panel host, Action toggleBorders, Action toggleCities)
    {
        this.host = host;
        borders = Button("Borders", $"Country borders on the map ({Settings.BordersKey.Value}). Display only: a mission's own zones and rules decide.", toggleBorders);
        cities = Button("Cities", $"Cities and bases on the map ({Settings.CitiesKey.Value}). Display only.", toggleCities);
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(10, 6, 0, 0),
            Height = 22,
        };
        row.Children.Add(borders);
        row.Children.Add(cities);
        // No background: hit tests fall through to the map everywhere but on the buttons.
        root = new Grid();
        root.Children.Add(row);
        host.Children.Add(root);
    }

    private static ToggleButton Button(string text, string tip, Action click)
    {
        var button = new ToggleButton
        {
            Focusable = false,
            Padding = new Thickness(6, 0, 6, 0),
            Margin = new Thickness(0, 0, 2, 0),
            VerticalAlignment = VerticalAlignment.Stretch,
            Content = new TextBlock { Text = text, FontSize = 11, VerticalAlignment = VerticalAlignment.Center },
            ToolTip = tip,
        };
        button.Click += (sender, args) =>
        {
            args.Handled = true;
            click();
        };
        return button;
    }

    public void Update(bool bordersOn, bool citiesOn)
    {
        if (borders.IsChecked != bordersOn)
            borders.IsChecked = bordersOn;
        if (cities.IsChecked != citiesOn)
            cities.IsChecked = citiesOn;
    }

    public void Detach()
    {
        try
        {
            host.Children.Remove(root);
        }
        catch (Exception ex)
        {
            ModLog.Debug($"Editor buttons detach: {ex.Message}");
        }
    }
}
