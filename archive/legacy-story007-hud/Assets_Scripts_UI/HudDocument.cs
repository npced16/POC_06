using System;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.UIElements;

namespace IronRing.UI
{
    /// <summary>UI Toolkit adapter for an injected HUD model; no per-frame system polling.</summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class HudDocument : MonoBehaviour
    {
        [Header("Action catalog")]
        [SerializeField] private string[] _facilityIds = Array.Empty<string>();
        [SerializeField] private int[] _slotIndices = Array.Empty<int>();
        private HudController _model;
        private VisualElement _root;
        private DropdownField _resource;
        private DropdownField _slot;
        private DropdownField _facility;

        /// <summary>Receives a model plus authored slot and facility catalogs, binds controls and renders the initial snapshot.</summary>
        public void Bind(HudController model, int[] slotIndices, string[] facilityIds)
        {
            Unbind();
            _model = model; _slotIndices = slotIndices; _facilityIds = facilityIds;
            _root = GetComponent<UIDocument>().rootVisualElement;
            _resource = _root.Q<DropdownField>("resource");
            _slot = _root.Q<DropdownField>("slot");
            _facility = _root.Q<DropdownField>("facility");
            var slots = new List<string>();
            foreach (int index in _slotIndices) slots.Add(index.ToString());
            _slot.choices = slots;
            _slot.SetValueWithoutNotify(slots.Count > 0 ? slots[0] : string.Empty);
            _facility.choices = new List<string>(_facilityIds);
            _facility.SetValueWithoutNotify(_facilityIds.Length > 0 ? _facilityIds[0] : string.Empty);
            _root.Q<Button>("place").clicked += Place;
            _root.Q<Button>("build").clicked += Build;
            _model.Changed += Render;
            Render();
        }
        /// <summary>Uses current dropdown choices to request placement through the model.</summary>
        private void Place() { if (int.TryParse(_slot.value, out int slot)) _model.Place(slot, _resource.value); }
        /// <summary>Uses the selected facility choice to request construction through the model.</summary>
        private void Build() { _model.Build(_facility.value); }

        /// <summary>Uses only cached presentation state to rebuild dynamic rows and update action availability.</summary>
        private void Render()
        {
            _root.Q<Label>("position").text = "CURRENT NODE  " + _model.CurrentNode + "     /     LAP  " + _model.Lap;
            var resources = _root.Q<VisualElement>("resources");
            resources.Clear();
            var choices = new List<string>();
            foreach (var pair in _model.Resources)
            {
                choices.Add(pair.Key);
                var label = new Label(pair.Key + "   " + pair.Value);
                label.AddToClassList("resource-chip"); resources.Add(label);
            }
            _resource.choices = choices;
            if (!choices.Contains(_resource.value)) _resource.SetValueWithoutNotify(choices.Count > 0 ? choices[0] : string.Empty);
            var hubs = _root.Q<ScrollView>("hubs");
            hubs.Clear();
            foreach (var pair in _model.Hubs)
            {
                var hub = pair.Value;
                var button = new Button(() => _model.SelectHub(hub.Id)) { name = "hub-" + hub.Id };
                button.AddToClassList("hub-row");
                button.EnableInClassList("ruined", hub.IsRuined);
                button.EnableInClassList("selected", hub.Id == _model.SelectedHub);
                button.Add(new Label(hub.Id + "    |    " + (hub.IsRuined ? "RUINED" : "ACTIVE") + "    |    Prosperity " + hub.Prosperity.ToString("0.#")));
                var details = new Label("Habitat " + (hub.HabitatReady ? "ready" : "incomplete") + "     NPCs " + hub.NpcCount + "     Facilities " + hub.FacilityCount);
                details.AddToClassList("muted"); button.Add(details); hubs.Add(button);
            }
            _root.Q<Label>("hubCount").text = "HUB NETWORK  /  " + _model.Hubs.Count + " hubs";
            _root.Q<Label>("selected").text = _model.SelectedHub ?? "No hub selected";
            _root.Q<Label>("message").text = _model.Message;
            _root.Q<VisualElement>("hud").EnableInClassList("lost", _model.IsLost);
            _root.Q<VisualElement>("defeat").EnableInClassList("hidden", !_model.IsLost);
            bool selected = _model.SelectedHub != null && !_model.IsLost;
            _root.Q<Button>("place").SetEnabled(selected && choices.Count > 0 && _slotIndices.Length > 0);
            _root.Q<Button>("build").SetEnabled(selected && _facilityIds.Length > 0);
        }
        /// <summary>Detaches model and button callbacks without disposing the caller-owned model.</summary>
        private void Unbind()
        {
            if (_model == null) return;
            _model.Changed -= Render;
            _root.Q<Button>("place").clicked -= Place;
            _root.Q<Button>("build").clicked -= Build;
            _model = null;
        }
        void OnDestroy() { Unbind(); }
    }
}
