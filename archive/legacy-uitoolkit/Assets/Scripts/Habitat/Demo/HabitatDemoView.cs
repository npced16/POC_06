using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;

using UnityEngine;
using UnityEngine.UIElements;

using IronRing.Inventory;

namespace IronRing.Habitat.Demo
{
    /// <summary>Story-local demo composition and UI; the fake track lets each configured hub be inspected independently.</summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class HabitatDemoView : MonoBehaviour
    {
        [Header("Authored Data")]
        [SerializeField] private TextAsset _configuration;
        [SerializeField] private StyleSheet _styleSheet;

        [Header("Capture")]
        [SerializeField] private int _captureAfterFrames = 60;

        private IReadOnlyList<HabitatDefinition> _definitions;
        private ResourceInventory _inventory;
        private HabitatEvaluator _habitat;
        private HabitatDemoTrack _track;
        private VisualElement _root;
        private int _selectedHub;
        private string _selectedResource;
        private string _message = "Docked and ready. Place cargo to assemble the habitat.";
        private int _flagEvents;

        void OnEnable()
        {
            var configuration = HabitatConfiguration.Parse(_configuration.text);
            _definitions = configuration.CreateDefinitions();
            _inventory = new ResourceInventory(configuration.CreateStock());
            var ids = new List<string>();
            foreach (var definition in _definitions) ids.Add(definition.HubId);
            _track = new HabitatDemoTrack(ids);
            _habitat = new HabitatEvaluator(_definitions, _inventory, _track);
            _habitat.EventFlagRaised += OnFlagRaised;
            _selectedResource = _definitions[0].AllowedResources[0];
            _root = GetComponent<UIDocument>().rootVisualElement;
            _root.style.unityFont = UnityEngine.Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _root.styleSheets.Add(_styleSheet);
            Refresh();
        }

        IEnumerator Start()
        {
            string capturePath = Argument("--story002-screenshot");
            if (capturePath == null && Argument("--scene") != "HabitatDemo") yield break;
            var definition = _definitions[_selectedHub];
            for (int slot = 0; slot < definition.SlotCount; slot++)
            {
                string resource = definition.Pattern[slot];
                if (resource != null && !_habitat.TryPlace(definition.HubId, slot, resource))
                    throw new InvalidOperationException("Demo could not complete its configured habitat.");
            }
            Refresh();
            if (capturePath == null) yield break;
            for (int frame = 0; frame < _captureAfterFrames; frame++) yield return null;
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(capturePath);
            for (int frame = 0; frame < 10; frame++) yield return null;
            string state = "hub=" + definition.HubId + " complete=" + _habitat.IsHabitatComplete(definition.HubId)
                + " flag=" + _habitat.IsEventFlagSet(definition.HubId) + " flagEvents=" + _flagEvents;
            File.WriteAllText(Path.ChangeExtension(capturePath, ".state.txt"), state);
            Debug.Log("Story002 capture: " + state);
            Application.Quit();
        }

        void OnDisable()
        {
            _habitat.EventFlagRaised -= OnFlagRaised;
        }

        /// <summary>Reads name from command line arguments and returns its following value, or null.</summary>
        private static string Argument(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++) if (args[i] == name) return args[i + 1];
            return null;
        }

        /// <summary>Records hubId's flag transition and updates the message shown on the next UI refresh.</summary>
        private void OnFlagRaised(string hubId)
        {
            _flagEvents++;
            _message = hubId + " event flag raised. A future visitor can now react to this habitat.";
        }

        /// <summary>Rebuilds the demo controls from selected hub, inventory quantities and live habitat state.</summary>
        private void Refresh()
        {
            var definition = _definitions[_selectedHub];
            var hubs = _root.Q<VisualElement>("hubs");
            hubs.Clear();
            for (int i = 0; i < _definitions.Count; i++)
            {
                int index = i;
                var button = new Button(() => SelectHub(index)) { text = _definitions[i].HubId.ToUpperInvariant() };
                button.EnableInClassList("selected", i == _selectedHub);
                hubs.Add(button);
            }
            _root.Q<Label>("dock").text = "DOCKED AT " + definition.HubId.ToUpperInvariant() + "   /   SELECT CARGO";
            var inventoryRow = _root.Q<VisualElement>("inventory");
            inventoryRow.Clear();
            foreach (string resource in definition.AllowedResources)
            {
                string id = resource;
                var button = new Button(() => SelectResource(id)) { text = id.ToUpperInvariant() + "  " + _inventory.Get(id) };
                button.EnableInClassList("selected", id == _selectedResource);
                inventoryRow.Add(button);
            }
            var slots = _root.Q<VisualElement>("slots");
            slots.Clear();
            for (int i = 0; i < definition.SlotCount; i++)
            {
                int index = i;
                string content = _habitat.SlotContent(definition.HubId, i);
                var slot = new VisualElement();
                slot.AddToClassList("slot");
                slot.EnableInClassList("slot-filled", content != null);
                var label = new Label("SLOT " + (i + 1));
                label.AddToClassList("slot-index");
                slot.Add(label);
                label = new Label((content ?? "empty").ToUpperInvariant());
                label.AddToClassList("resource");
                slot.Add(label);
                slot.Add(new Button(() => EditSlot(index)) { text = content == null ? "Place cargo" : "Reclaim" });
                label = new Label("Recipe: " + (definition.Pattern[i] ?? "keep empty"));
                label.AddToClassList("requirement");
                slot.Add(label);
                slots.Add(slot);
            }
            bool complete = _habitat.IsHabitatComplete(definition.HubId);
            var status = _root.Q<Label>("completion");
            status.text = complete ? "HABITAT COMPLETE" : "HABITAT INCOMPLETE";
            status.EnableInClassList("complete", complete);
            status.EnableInClassList("incomplete", !complete);
            _root.Q<Label>("flag").text = "EVENT FLAG: " + (_habitat.IsEventFlagSet(definition.HubId) ? "RAISED" : "WAITING")
                + "     /     POLICY: " + (definition.FlagSticky ? "STICKY" : "MATCH CURRENT COMBO");
            _root.Q<Label>("message").text = _message;
        }

        /// <summary>Selects configured hub index, docks the fake track there and refreshes the controls.</summary>
        private void SelectHub(int index)
        {
            _selectedHub = index;
            _track.DockAt(_definitions[index].HubId);
            _selectedResource = _definitions[index].AllowedResources[0];
            Refresh();
        }

        /// <summary>Selects resourceId for subsequent empty-slot placement and refreshes its highlight.</summary>
        private void SelectResource(string resourceId)
        {
            _selectedResource = resourceId;
            Refresh();
        }

        /// <summary>Places selected cargo or reclaims existing cargo at slotIndex, then displays the resulting state.</summary>
        private void EditSlot(int slotIndex)
        {
            string hubId = _definitions[_selectedHub].HubId;
            bool occupied = _habitat.SlotContent(hubId, slotIndex) != null;
            _message = occupied ? "Cargo returned to inventory." : "Cargo placed.";
            bool success = occupied ? _habitat.TryRemove(hubId, slotIndex) : _habitat.TryPlace(hubId, slotIndex, _selectedResource);
            if (!success) _message = "Placement refused. Check stock, slot and dock position.";
            Refresh();
        }
    }
}
