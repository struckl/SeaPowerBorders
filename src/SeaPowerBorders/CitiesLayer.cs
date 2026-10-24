using System;
using System.ComponentModel;
using SeaPower;
using SeapowerUI;

namespace SeaPowerBorders;

/// <summary>
/// The "Cities" entry in the map's Layers menu (the menu lists <c>GizmoLayerManager.Layers</c>, so one more
/// <see cref="MapLayer"/> there gets a checkbox like the stock layers). Its state is kept in our config.
/// </summary>
internal sealed class CitiesLayer
{
    private const int PreferredNumber = 602;          // Better Tac Map uses 500-509, Topographic Map 600, Borders 601
    private const string Name = "SEWERLABS_CITIES";
    private const string DisplayName = "Cities and bases";

    private MapLayer layer;
    private bool failed;

    public bool Visible => layer?.Visible ?? Settings.CitiesVisible.Value;

    /// <summary>Adds the entry once the game's layer manager exists (it lives for the whole session).</summary>
    public void EnsureRegistered()
    {
        if (layer != null || failed)
            return;
        try
        {
            GizmoLayerManager manager = GizmoLayerManager.Instance;
            if (manager.LayersByName.TryGetValue(Name, out MapLayer existing))
            {
                Adopt(existing);
                return;
            }
            int number = PreferredNumber;
            while (manager.LayersById.ContainsKey(number))
                number++;

            // MapLayer looks its menu text up in the language table; give it ours first.
            Singleton<LanguageResourceHandler>.Instance.LanguageDictionary[("MapLayers" + Name).ToLowerInvariant()] = DisplayName;
            var created = new MapLayer(number, Name, true);
            manager.Layers.Add(created);
            manager.LayersByName[Name] = created;
            manager.LayersById[number] = created;
            Adopt(created);
            ModLog.Info($"Map layer '{DisplayName}' added to the Layers menu (#{number}), {(created.Visible ? "on" : "off")}.");
        }
        catch (Exception ex)
        {
            failed = true;
            ModLog.Error($"Could not add the map layer, use the toggle key instead: {ex}");
        }
    }

    public void Toggle()
    {
        if (layer != null)
            layer.Visible = !layer.Visible;
        else
            Settings.CitiesVisible.Value = !Settings.CitiesVisible.Value;
    }

    private void Adopt(MapLayer l)
    {
        layer = l;
        l.Visible = Settings.CitiesVisible.Value;
        l.PropertyChanged += OnLayerChanged;
    }

    private void OnLayerChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MapLayer.Visible) && layer != null && Settings.CitiesVisible.Value != layer.Visible)
            Settings.CitiesVisible.Value = layer.Visible;
    }
}
