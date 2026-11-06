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
        private float _edgeSeconds;
        private int _edgeIndex = -1;
        private float EdgeSeconds => _edgeSeconds;
        /// <summary>Initialized movement model for location queries and arrival subscriptions.</summary>
        public RingTrack Track => _track;
        /// <summary>출발 구간 번호를 받아 기본 구간 시간에 곱할 배율을 돌려준다. 비어 있으면 1이다. 배율은 출발할 때 한 번 적용한다.</summary>
        public Func<int, float> EdgeDurationScale { get; set; }
        /// <summary>Normalized progress along the active edge, or zero while standing at a node.</summary>
        public float TravelProgress => _track.IsMoving ? Mathf.Clamp01(_elapsed / EdgeSeconds)
            : _headStart > 0 ? _headStart : 0;

        /// <summary>Story 033: the next departure starts this far (0..0.95) into the edge so the train continues from where the raid crawl left it.</summary>
        public void SetDepartureHeadStart(float fraction) => _headStart = Mathf.Clamp(fraction, 0f, 0.95f);

        void Awake()
        {
            if (_configuration == null) _configuration = UnityEngine.Resources.Load<TextAsset>("Track/ring-track");
            _data = TrackData.Parse(_configuration.text);
            _track = _data.CreateTrack();
            _edgeSeconds = _data.EdgeSeconds;
        }

        void Update()
        {
            if (_track.HasActiveHolds) return;
            if (!_track.IsMoving)
            {
                _waiting += Time.deltaTime;
                bool requested = _keyboardDeparture && Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame && !IronRing.Hud.Onboarding.DevSituationsScreen.IsOpen && !IronRing.Hud.GameMenuScreen.IsOpen && (IronRing.Onboarding.OnboardingInstaller.Director == null || IronRing.Onboarding.OnboardingInstaller.Director.BasicTutorialCompleted); // 개발자 메뉴와 게임 메뉴가 열렸거나 기본 학습이 끝나지 않으면 Space 출발을 막는다.
                bool demonstration = _autoDepart || _data.AutoDepart || Argument("--track-demo") != null;
                if (requested || !_track.IsStopped || (demonstration && _waiting >= _data.StationWaitSeconds)) Depart();
                return;
            }

            // 공용 API 출발과 잠긴 역 통과도 새 구간으로 보고 이동 시간을 다시 정한다.
            if (_track.CurrentIndex != _edgeIndex) BeginEdge();
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
                _edgeIndex = -1;
            }
        }

        /// <summary>Requests one edge of travel and resets interpolation only when the movement model accepts departure.</summary>
        public void Depart()
        {
            if (!_track.TryDepart()) return;
            BeginEdge();
            _elapsed = _headStart * EdgeSeconds;
            _headStart = 0;
        }

        /// <summary>
        /// 현재 구간 번호로 EdgeDurationScale을 불러 이번 구간의 이동 시간을 정한다.
        /// _track.CurrentIndex와 기본 구간 시간을 사용하며, _edgeSeconds와 _edgeIndex를 변경한다.
        /// </summary>
        private void BeginEdge()
        {
            // 구간 시간은 구간을 시작할 때 정해서 이동 중 선로가 넓어져도 진행도가 튀지 않게 한다.
            _edgeIndex = _track.CurrentIndex;
            float scale = EdgeDurationScale != null ? EdgeDurationScale(_edgeIndex) : 1f;
            _edgeSeconds = _data.EdgeSeconds * Mathf.Max(0.01f, scale);
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
