using UnityEngine;

namespace IronRing.Defense.Demo
{
    /// <summary>Isolated hub presentation backed by real facility construction and local interface fakes.</summary>
    public sealed class FacilityDemoView : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private TextAsset _catalogData;
        [SerializeField] private string _hubId = "Foundry Hub";
        [SerializeField] private bool _buildOnStart = true;
        [Header("Presentation")]
        [SerializeField] private Vector2 _referenceSize = new Vector2(1280, 720);
        [SerializeField] private Color _background = new Color(0.055f, 0.085f, 0.11f);
        [SerializeField] private Color _brass = new Color(0.85f, 0.66f, 0.35f);
        private FacilityBuilder _builder;
        private DefenseDemoHubs _hubs;
        private FacilityCatalog _catalog;
        private int _events;

        void Awake()
        {
            _catalog = FacilityCatalog.Parse(_catalogData.text);
            _hubs = new DefenseDemoHubs(_hubId);
            var inventory = new DefenseDemoInventory();
            foreach (var definition in _catalog.CreateDefinitions())
                foreach (var cost in definition.Costs) inventory.Add(cost.Key, cost.Value);
            _builder = new FacilityBuilder(_catalog.CreateDefinitions(), _catalog.PerHubLimit, _catalog.AllowBuildInRuins, inventory, _hubs, id => id == _hubId);
            _builder.FacilityBuilt += OnFacilityBuilt;
            if (_buildOnStart)
                foreach (var definition in _catalog.CreateDefinitions()) _builder.TryBuild(_hubId, definition.Id);
        }

        /// <summary>Counts successful facilityId construction events at hubId for the demo readout.</summary>
        private void OnFacilityBuilt(string hubId, string facilityId) { _events++; }

        void OnGUI()
        {
            GUI.matrix = Matrix4x4.Scale(new Vector3(Screen.width / _referenceSize.x, Screen.height / _referenceSize.y, 1f));
            Fill(new Rect(0, 0, 1280, 720), _background);
            Label(new Rect(60, 35, 700, 35), "IRON RING  /  DEFENSE FACILITIES", 19, _brass);
            Label(new Rect(60, 84, 900, 65), _hubId.ToUpperInvariant(), 42, Color.white);
            Label(new Rect(62, 148, 800, 30), "TRAIN DOCKED     /     BUILD COMPLETE     /     LIVE HUB", 17, new Color(0.47f, 0.79f, 0.72f));
            Fill(new Rect(60, 203, 1160, 2), _brass);

            var definitions = _catalog.CreateDefinitions();
            var built = _builder.FacilitiesAt(_hubId);
            for (int i = 0; i < built.Count; i++)
            {
                float x = 60 + i * 395;
                FacilityDefinition definition = null;
                foreach (var candidate in definitions) if (candidate.Id == built[i]) definition = candidate;
                Fill(new Rect(x, 235, 370, 310), new Color(0.095f, 0.135f, 0.16f));
                Fill(new Rect(x + 28, 390, 314, 5), _brass);
                DrawFacility(new Rect(x + 80, 265, 210, 125), definition.Id);
                Label(new Rect(x + 26, 417, 320, 35), definition.Id.ToUpperInvariant(), 26, Color.white);
                string effect = definition.IsProsperityFacility ? "PROSPERITY  +" + definition.ProsperityDelta : "COMBAT  +" + definition.CombatBonus;
                Label(new Rect(x + 26, 465, 320, 30), effect, 21, _brass);
                Label(new Rect(x + 26, 503, 320, 25), "INSTALLED  /  " + (i + 1).ToString("00"), 14, new Color(0.47f, 0.79f, 0.72f));
            }
            Label(new Rect(60, 580, 650, 35), "HUB DEFENSE  +" + _builder.CombatBonus(_hubId) + "     PROSPERITY  +" + _hubs.GetProsperity(_hubId), 24, Color.white);
            Label(new Rect(60, 638, 1100, 35), "CAPACITY  " + built.Count + " / " + _catalog.PerHubLimit + "     |     BUILD EVENTS  " + _events + "     |     RUIN BUILDING  " + (_catalog.AllowBuildInRuins ? "ALLOWED" : "DISABLED"), 17, _brass);
        }

        /// <summary>Draws a facility silhouette inside area using id to select its structural form.</summary>
        private void DrawFacility(Rect area, string id)
        {
            Color metal = new Color(0.39f, 0.48f, 0.5f);
            if (id == "wall")
            {
                Fill(new Rect(area.x, area.y + 45, area.width, 80), metal);
                for (int i = 0; i < 4; i++) Fill(new Rect(area.x + i * 55, area.y + 20, 40, 30), _brass);
                for (int i = 0; i < 3; i++) Fill(new Rect(area.x + 25 + i * 65, area.y + 70, 25, 10), _background);
            }
            else if (id == "turret")
            {
                Fill(new Rect(area.x + 60, area.y + 65, 90, 60), metal);
                Fill(new Rect(area.x + 35, area.y + 40, 130, 40), _brass);
                Fill(new Rect(area.x + 115, area.y + 47, 100, 16), metal);
                Fill(new Rect(area.x + 35, area.y + 110, 140, 15), _brass);
            }
            else
            {
                Fill(new Rect(area.x + 15, area.y + 40, 175, 85), metal);
                Fill(new Rect(area.x + 145, area.y, 25, 60), _brass);
                Fill(new Rect(area.x, area.y + 32, 205, 15), _brass);
                Fill(new Rect(area.x + 38, area.y + 65, 35, 30), _brass);
                Fill(new Rect(area.x + 110, area.y + 65, 45, 60), _background);
            }
        }

        /// <summary>Fills rect with color and restores GUI tint after drawing.</summary>
        private void Fill(Rect rect, Color color) { GUI.color = color; GUI.DrawTexture(rect, Texture2D.whiteTexture); GUI.color = Color.white; }

        /// <summary>Draws text in rect at the supplied size and color without changing shared GUI styles.</summary>
        private void Label(Rect rect, string text, int size, Color color)
        {
            var style = new GUIStyle(GUI.skin.label) { fontSize = size, fontStyle = FontStyle.Bold };
            style.normal.textColor = color;
            GUI.Label(rect, text, style);
        }
    }
}
