using System;
using System.Collections.Generic;
using System.ComponentModel;
using BepInEx.Configuration;
using SeaPower;
using SeapowerUI;

namespace SeaPowerBorders;

/// <summary>
/// An entry in the map's Layers menu (the menu lists <c>MapDisplayState.Tactical.Layers</c>, so one more
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
#if FLATMAP
            // Sea Power 0.8.7: one layer manager for the whole game.
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
#else
            // The 3D-globe build: every map has its own display state with its own layer list.
            MapDisplayState state = MapDisplayState.Tactical;
            if (state.LayersByName.TryGetValue(name, out MapLayer existing))
            {
                Adopt(existing);
                return;
            }
            int number = preferredNumber;
            while (state.LayersById.ContainsKey(number))
                number++;

            // MapLayer looks its menu text up in the language table; give it ours first.
            Singleton<LanguageResourceHandler>.Instance.LanguageDictionary[("MapLayers" + name).ToLowerInvariant()] = displayName;
            MapLayer created = GameApi.NewLayer(state, number, name, true);
            if (created == null || !(state.LayersByName is IDictionary<string, MapLayer> byName) ||
                !(state.LayersById is IDictionary<int, MapLayer> byId))
            {
                failed = true;
                ModLog.Warn($"The map's layer list has changed - no '{displayName}' entry in the Layers menu; the toggle key still works.");
                return;
            }
            state.Layers.Add(created);
            byName[name] = created;
            byId[number] = created;
            Adopt(created);
#endif
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
