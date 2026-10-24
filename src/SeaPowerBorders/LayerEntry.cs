using System;
using System.ComponentModel;
using BepInEx.Configuration;
using SeaPower;
using SeapowerUI;

namespace SeaPowerBorders;

/// <summary>
/// An entry in the map's Layers menu (the menu lists <c>GizmoLayerManager.Layers</c>, so one more
/// <see cref="MapLayer"/> there gets a checkbox like the stock layers). Its state is kept in our config.
/// Numbers: Better Tac Map uses 500-509, Topographic Map 600, Borders 601, Cities 602.
/// </summary>
internal sealed class LayerEntry
{
    private readonly int preferredNumber;
    private readonly string name, displayName;
    private readonly ConfigEntry<bool> visible;

    private MapLayer layer;
    private bool failed;

    public LayerEntry(int preferredNumber, string name, string displayName, ConfigEntry<bool> visible)
    {
        this.preferredNumber = preferredNumber;
        this.name = name;
        this.displayName = displayName;
        this.visible = visible;
    }

    public bool Visible => layer?.Visible ?? visible.Value;

    /// <summary>Adds the entry once the game's layer manager exists (it lives for the whole session).</summary>
    public void EnsureRegistered()
    {
        if (layer != null || failed)
            return;
        try
        {
            GizmoLayerManager manager = GizmoLayerManager.Instance;
            if (manager.LayersByName.TryGetValue(name, out MapLayer existing))
            {
                Adopt(existing);
                return;
            }
            int number = preferredNumber;
            while (manager.LayersById.ContainsKey(number))
                number++;

            // MapLayer looks its menu text up in the language table; give it ours first.
            Singleton<LanguageResourceHandler>.Instance.LanguageDictionary[("MapLayers" + name).ToLowerInvariant()] = displayName;
            var created = new MapLayer(number, name, true);
            manager.Layers.Add(created);
            manager.LayersByName[name] = created;
            manager.LayersById[number] = created;
            Adopt(created);
            ModLog.Info($"Map layer '{displayName}' added to the Layers menu (#{number}), {(created.Visible ? "on" : "off")}.");
        }
        catch (Exception ex)
        {
            failed = true;
            ModLog.Error($"Could not add the map layer '{displayName}', use the toggle key instead: {ex}");
        }
    }

    public void Toggle()
    {
        if (layer != null)
            layer.Visible = !layer.Visible;
        else
            visible.Value = !visible.Value;
    }

    private void Adopt(MapLayer l)
    {
        layer = l;
        l.Visible = visible.Value;
        l.PropertyChanged += OnLayerChanged;
    }

    private void OnLayerChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MapLayer.Visible) && layer != null && visible.Value != layer.Visible)
            visible.Value = layer.Visible;
    }
}
