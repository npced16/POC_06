using System;

using UnityEngine;

using IronRing.Contracts;

namespace IronRing.Npc.Demo
{
    /// <summary>Isolated, data-configured recruitment presentation for Story 003 run-and-observe.</summary>
    public sealed class NpcDemoView : MonoBehaviour
    {
        [Header("Data and deterministic demo visit")]
        [SerializeField] private TextAsset _poolJson;
        [SerializeField] private TextAsset _visitJson;
        private NpcRecruiter _recruiter;
        private string _name = "Awaiting arrival";
        private string _quote = "";
        private string _hub = "";
        private NpcPoolData _pool;

        [Header("Presentation")]
        [SerializeField] private Rect _panel = new Rect(120, 110, 1040, 500);
        [SerializeField] private Color _background = new Color(0.055f, 0.075f, 0.09f);
        [SerializeField] private Color _accent = new Color(0.9f, 0.7f, 0.37f);
        [SerializeField] private int _nameSize = 54;
        [SerializeField] private int _quoteSize = 28;

        void Start()
        {
            _pool = NpcPoolData.FromJson(_poolJson.text);
            var visit = JsonUtility.FromJson<VisitData>(_visitJson.text);
            _hub = visit.HubId;
            _recruiter = new NpcRecruiter(_pool.Definitions, _pool.JoinChance, new DemoHabitat(visit.Flagged, visit.Complete), new DemoRandom(visit.Roll, visit.Selection));
            _recruiter.NpcJoined += ShowJoined;
            _recruiter.OnHubVisited(_hub);
        }

        void OnDestroy()
        {
            if (_recruiter != null) _recruiter.NpcJoined -= ShowJoined;
        }

        /// <summary>Finds the NPC named by the committed join event and updates the visible name and quote.</summary>
        private void ShowJoined(string hubId, string npcId)
        {
            foreach (var npc in _pool.Definitions)
                if (npc.Id == npcId) { _name = npc.Name; _quote = npc.Quote; break; }
        }

        void OnGUI()
        {
            GUI.color = _background;
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Box(_panel, GUIContent.none);
            var label = new GUIStyle(GUI.skin.label) { fontSize = 20, wordWrap = true };
            label.normal.textColor = _accent;
            GUI.Label(new Rect(_panel.x + 48, _panel.y + 38, _panel.width - 96, 38), "IRON RING  /  NEW ARRIVAL", label);
            label.fontSize = _nameSize;
            label.normal.textColor = Color.white;
            GUI.Label(new Rect(_panel.x + 48, _panel.y + 116, _panel.width - 96, 85), _name, label);
            label.fontSize = _quoteSize;
            label.normal.textColor = new Color(0.88f, 0.89f, 0.86f);
            GUI.Label(new Rect(_panel.x + 48, _panel.y + 220, _panel.width - 96, 120), "\u201c" + _quote + "\u201d", label);
            label.fontSize = 20;
            label.normal.textColor = _accent;
            GUI.Label(new Rect(_panel.x + 48, _panel.y + 402, _panel.width - 96, 40), "JOINED " + _hub.ToUpperInvariant() + "   /   A NEW HOME ON THE RING", label);
        }

        [Serializable]
        private sealed class VisitData
        {
            [Header("Visit inputs")]
            [SerializeField] private string _hubId;
            [SerializeField] private bool _flagged;
            [SerializeField] private bool _complete;
            [SerializeField] private float _roll;
            [SerializeField] private int _selection;
            public string HubId => _hubId;
            public bool Flagged => _flagged;
            public bool Complete => _complete;
            public float Roll => _roll;
            public int Selection => _selection;
        }

        private sealed class DemoRandom : IRandomSource
        {
            private readonly float _roll;
            private readonly int _selection;
            /// <summary>Stores data-defined deterministic draws for the retained demo.</summary>
            public DemoRandom(float roll, int selection) { _roll = roll; _selection = selection; }
            /// <summary>Returns the demo probability input without changing production random state.</summary>
            public float NextFloat() => _roll;
            /// <summary>Returns the data-defined selection constrained to the requested range.</summary>
            public int NextInt(int minInclusive, int maxExclusive) => Math.Min(Math.Max(_selection, minInclusive), maxExclusive - 1);
        }

        private sealed class DemoHabitat : IHabitatSystem
        {
            private readonly bool _flagged;
            private readonly bool _complete;
            public event Action<string> EventFlagRaised { add { } remove { } }
            /// <summary>Stores data-defined flag and completion values for the isolated demo hub.</summary>
            public DemoHabitat(bool flagged, bool complete) { _flagged = flagged; _complete = complete; }
            /// <summary>Returns the configured event flag for the demo hub input.</summary>
            public bool IsEventFlagSet(string hubId) => _flagged;
            /// <summary>Returns the configured habitat completion for the demo hub input.</summary>
            public bool IsHabitatComplete(string hubId) => _complete;
            /// <summary>Refuses all placement inputs because the demo consumes habitat state only.</summary>
            public bool TryPlace(string hubId, int slotIndex, string resourceId) => false;
            /// <summary>Refuses all removal inputs because the demo consumes habitat state only.</summary>
            public bool TryRemove(string hubId, int slotIndex) => false;
            /// <summary>Returns an empty slot for any hub and slot input in the display-only fake.</summary>
            public string SlotContent(string hubId, int slotIndex) => null;
        }
    }
}
