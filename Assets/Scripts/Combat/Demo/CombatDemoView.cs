using System;

using UnityEngine;

using IronRing.Contracts;

namespace IronRing.Combat.Demo
{
    public sealed class CombatDemoView : MonoBehaviour
    {
        [Header("Encounter presentation")]
        [SerializeField] private TextAsset _encounterData;
        [SerializeField] private float _roundSeconds = 2f;
        [SerializeField] private float _initialProsperity = 50f;
        [SerializeField] private int _initialStock = 5;
        [SerializeField] private Rect _panel = new Rect(60, 55, 1160, 600);
        private WaypointResolver _resolver;
        private CombatDemoTrack _track;
        private CombatDemoInventory _inventory;
        private CombatDemoHubs _hubs;
        private float _nextRound;
        private string _roundLog;
        private bool _captureResult;
        private EncounterData _data;

        void Start()
        {
            _captureResult = Array.IndexOf(Environment.GetCommandLineArgs(), "--combat-result") >= 0;
            Begin(Array.IndexOf(Environment.GetCommandLineArgs(), "--combat-defeat") >= 0);
            Advance();
            if (_captureResult) { while (_resolver.IsEncounterActive) Advance(); }
        }

        void Update()
        {
            if (!_resolver.IsEncounterActive || Time.time < _nextRound) return;
            Advance();
            _nextRound = Time.time + _roundSeconds;
        }

        void OnDestroy() { _resolver?.Dispose(); }

        /// <summary>Creates demo interface fakes, wires track arrivals and starts a scripted win or loss using the authored encounter data.</summary>
        private void Begin(bool defeat)
        {
            _resolver?.Dispose();
            EncounterData data = JsonUtility.FromJson<EncounterData>(_encounterData.text);
            _data = data;
            var waypoint = new CombatDemoNode { Id = "Ash Crossing", Index = 1, Kind = TrackNodeKind.Waypoint };
            _track = new CombatDemoTrack(new CombatDemoNode { Id = "Foundry", HubId = "Foundry", Kind = TrackNodeKind.Hub }, waypoint);
            _inventory = new CombatDemoInventory();
            foreach (string resource in data.PenaltyResources) _inventory.Add(resource, _initialStock);
            _hubs = new CombatDemoHubs(_initialProsperity, "Foundry");
            float roll = defeat ? 0.99f : 0f;
            var rolls = new float[data.Rounds + 1];
            rolls[0] = 0.99f;
            for (int i = 1; i < rolls.Length; i++) rolls[i] = roll;
            var random = new CombatDemoRandom(0, rolls);
            _resolver = new WaypointResolver(data, _track, _inventory, _hubs, new CombatDemoFacilities(), random);
            _track.Arrived += _resolver.OnWaypointArrived;
            _track.Arrive(waypoint);
            _roundLog = "";
            _nextRound = Time.time + _roundSeconds;
        }

        /// <summary>Advances the resolver one round and appends its actual result to the visible combat log.</summary>
        private void Advance()
        {
            _resolver.Advance();
            _roundLog += _resolver.Combat == null ? _resolver.EventTitle :
                $"Round {_resolver.Combat.Round}   {(_resolver.Combat.LastRoundWon ? "TRAIN WINS" : "RAIDERS WIN")}\n";
        }

        void OnGUI()
        {
            if (_resolver == null) return;
            GUI.backgroundColor = new Color(0.12f, 0.18f, 0.22f);
            GUI.Box(_panel, GUIContent.none);
            var title = new GUIStyle(GUI.skin.label) { fontSize = 32, fontStyle = FontStyle.Bold };
            var text = new GUIStyle(GUI.skin.label) { fontSize = 23 };
            var accent = new GUIStyle(text);
            accent.normal.textColor = new Color(1f, 0.75f, 0.34f);
            float x = _panel.x + 35, y = _panel.y + 25;
            GUI.Label(new Rect(x, y, 1050, 50), "IRON RING  /  ASH CROSSING", title);
            GUI.Label(new Rect(x, y + 65, 1050, 40), _resolver.IsEncounterActive ? "RAIDER ENCOUNTER  -  COMBAT IN PROGRESS" : $"ENCOUNTER COMPLETE  -  {_resolver.LastOutcome.ToString().ToUpperInvariant()}", accent);
            string matchup = _resolver.Combat == null ? _resolver.EventTitle :
                $"Train strength {_resolver.Combat.TrainStrength}     vs     Raider strength {_resolver.Combat.EnemyStrength}     |     Best of {_resolver.Combat.TotalRounds}";
            GUI.Label(new Rect(x, y + 115, 1050, 40), matchup, text);
            GUI.Label(new Rect(x, y + 165, 1050, 135), string.IsNullOrEmpty(_roundLog) ? "Weapons ready. First round approaching..." : _roundLog, text);
            GUI.Label(new Rect(x, y + 315, 1050, 40), $"TRAIN: {(_track.IsHeld ? "HELD AT WAYPOINT" : "RELEASED - ROUTE CONTINUES")}", accent);
            string stock = "";
            foreach (string resource in _data.PenaltyResources) stock += $"{resource} {_inventory.Get(resource)}    ";
            GUI.Label(new Rect(x, y + 365, 1050, 40), $"{stock} | Next hub prosperity {_hubs.GetProsperity("Foundry")}", text);
            GUI.Label(new Rect(x, y + 410, 1050, 40), $"Victory: +{_data.RewardAmount} {_data.RewardResource}    /    Defeat: -{_data.PenaltyAmount} random resource, {_data.ProsperityPenalty} next hub prosperity", text);
            if (!_resolver.IsEncounterActive)
            {
                if (GUI.Button(new Rect(x, y + 485, 230, 45), "Replay victory")) Begin(false);
                if (GUI.Button(new Rect(x + 250, y + 485, 230, 45), "Replay defeat")) Begin(true);
            }
        }
    }
}
