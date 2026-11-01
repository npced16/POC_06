using System;
using System.Collections;

using UnityEngine;
using UnityEngine.InputSystem;

namespace IronRing.Track
{
    /// <summary>Displays authored ring data and interpolates one edge while the pure model owns arrival rules.</summary>
    public sealed class TrainController : MonoBehaviour
    {
        [Header("Track Configuration")]
        [SerializeField] private TextAsset _configuration;
        [Header("Demo")]
        [SerializeField] private bool _autoDepart;
        [Header("Standalone presentation")]
        [SerializeField] private bool _showPresentation = true;
        [SerializeField] private bool _keyboardDeparture = true;
        private TrackData _data;
        private RingTrack _track;
        private float _elapsed;
        private float _waiting;
        private bool _captureStarted;
        private float _headStart;
        /// <summary>
        /// Story 033: while the track is held at a waypoint (raid), this supplies the visual progress along the outgoing edge so the
        /// train crawls through the occupied segment; null = stand on the node as before.
        /// </summary>
        public Func<float> HeldProgressSource { get; set; }
        /// <summary>Story 037: when above zero, one edge takes this many seconds instead of the track data (the side-scrolling travel view sets it from travel-side.json).</summary>
        public float EdgeSecondsOverride { get; set; }
        private float EdgeSeconds => EdgeSecondsOverride > 0f ? EdgeSecondsOverride : _data.EdgeSeconds;
        /// <summary>Initialized movement model for location queries and arrival subscriptions.</summary>
        public RingTrack Track => _track;
        /// <summary>Normalized progress along the active edge, or zero while standing at a node.</summary>
        public float TravelProgress => _track.IsMoving ? Mathf.Clamp01(_elapsed / EdgeSeconds)
            : _headStart > 0 ? _headStart : HeldProgressSource != null && !_track.IsStopped ? Mathf.Clamp01(HeldProgressSource()) : 0;

        /// <summary>Story 033: the next departure starts this far (0..0.95) into the edge so the train continues from where the raid crawl left it.</summary>
        public void SetDepartureHeadStart(float fraction) => _headStart = Mathf.Clamp(fraction, 0f, 0.95f);

        void Awake()
        {
            if (_configuration == null) _configuration = UnityEngine.Resources.Load<TextAsset>("Track/ring-track");
            _data = TrackData.Parse(_configuration.text);
            _track = _data.CreateTrack();
        }

        void Update()
        {
            if (_track.HasActiveHolds) return;
            if (!_track.IsMoving)
            {
                _waiting += Time.deltaTime;
                bool requested = _keyboardDeparture && Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
                bool demonstration = _autoDepart || _data.AutoDepart || Argument("--track-demo") != null;
                if (requested || !_track.IsStopped || (demonstration && _waiting >= _data.StationWaitSeconds)) Depart();
                return;
            }

            _elapsed += Time.deltaTime;
            if (!_captureStarted && Argument("--track-screenshot") != null && Time.frameCount >= 60 && TravelProgress >= 0.4f && TravelProgress < 0.9f)
            {
                _captureStarted = true;
                StartCoroutine(Capture());
            }
            if (_elapsed >= EdgeSeconds)
            {
                _track.Arrive();
                _elapsed = 0;
                _waiting = 0;
            }
        }

        /// <summary>Requests one edge of travel and resets interpolation only when the movement model accepts departure.</summary>
        public void Depart()
        {
            if (_track.TryDepart()) { _elapsed = _headStart * EdgeSeconds; _headStart = 0; }
        }

        /// <summary>Configures the standalone view and keyboard option during scene creation so an integrated HUD can own presentation and input.</summary>
        public void ConfigurePresentation(bool showPresentation, bool keyboardDeparture)
        {
            _showPresentation = showPresentation;
            _keyboardDeparture = keyboardDeparture;
        }

        void OnGUI()
        {
            if (!_showPresentation) return;
            Matrix4x4 previous = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(Screen.width / _data.ReferenceSize.x, Screen.height / _data.ReferenceSize.y, 1));
            Fill(new Rect(Vector2.zero, _data.ReferenceSize), _data.Background);
            var label = new GUIStyle(GUI.skin.label) { fontSize = _data.FontSize, alignment = TextAnchor.MiddleCenter };
            label.normal.textColor = _data.Station;
            GUI.Label(new Rect(0, 24, _data.ReferenceSize.x, 48), "IRON RING  /  ARMOURED RAIL", label);
            for (int i = 0; i < _data.Nodes.Count; i++)
            {
                Vector2 from = _data.Nodes[i].Position;
                Vector2 to = _data.Nodes[(i + 1) % _data.Nodes.Count].Position;
                Matrix4x4 saved = GUI.matrix;
                GUIUtility.RotateAroundPivot(Mathf.Atan2(to.y - from.y, to.x - from.x) * Mathf.Rad2Deg, from);
                Fill(new Rect(from.x, from.y - _data.RailWidth / 2, Vector2.Distance(from, to), _data.RailWidth), _data.Rail);
                GUI.matrix = saved;
            }
            foreach (NodeData node in _data.Nodes)
            {
                float size = node.IsStation ? _data.StationSize : _data.WaypointSize;
                Fill(new Rect(node.Position - Vector2.one * size / 2, Vector2.one * size), node.IsStation ? _data.Station : _data.Waypoint);
                GUI.Label(new Rect(node.Position.x - 120, node.Position.y + size / 2 + 8, 240, 36), node.Id, label);
            }
            Vector2 position = Vector2.Lerp(_data.Nodes[_track.CurrentIndex].Position, _data.Nodes[_track.NextIndex].Position, TravelProgress);
            Fill(new Rect(position - _data.TrainSize / 2, _data.TrainSize), _data.Train);
            Fill(new Rect(position.x, position.y - _data.TrainSize.y / 3, _data.TrainSize.x / 3, _data.TrainSize.y * 2 / 3), _data.Background);
            GUI.Label(new Rect(0, _data.ReferenceSize.y - 112, _data.ReferenceSize.x, 36),
                $"LAP {_track.LapCount}   /   {_track.CurrentNode.Id}   /   {(_track.IsMoving ? "TRAVELLING" : _track.IsStopped ? "STATION STOP" : "WAYPOINT")}", label);
            GUI.enabled = !_track.IsMoving && !_track.HasActiveHolds;
            if (GUI.Button(new Rect(_data.ReferenceSize.x / 2 - 130, _data.ReferenceSize.y - 64, 260, 40), "DEPART  [SPACE]")) Depart();
            GUI.enabled = true;
            GUI.matrix = previous;
        }

        /// <summary>Draws a rectangle using the supplied bounds and colour, restoring GUI colour after drawing.</summary>
        private static void Fill(Rect bounds, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(bounds, Texture2D.whiteTexture);
            GUI.color = previous;
        }

        /// <summary>Reads a named command-line argument and returns its following value or null when absent.</summary>
        private static string Argument(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++) if (args[i] == name) return args[i + 1];
            return null;
        }

        /// <summary>Captures a rendered in-transit frame to the requested absolute path, then exits the evidence player.</summary>
        private IEnumerator Capture()
        {
            yield return new WaitForEndOfFrame();
            UnityEngine.Debug.Log($"Track evidence: {_track.CurrentNode.Id} -> {_track.Nodes[_track.NextIndex].Id}, progress={TravelProgress:F3}, lap={_track.LapCount}");
            ScreenCapture.CaptureScreenshot(Argument("--track-screenshot"));
            for (int i = 0; i < 5; i++) yield return null;
            Application.Quit();
        }
    }
}
