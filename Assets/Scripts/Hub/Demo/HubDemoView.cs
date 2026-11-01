using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;

using UnityEngine;

namespace IronRing.Hub.Demo
{
    /// <summary>Isolated hub demo with a permanent ring, selectable stops, data growth and visible rubble.</summary>
    public sealed class HubDemoView : MonoBehaviour
    {
        [Header("Simulation data")]
        [SerializeField] private TextAsset _profiles;
        [SerializeField] private int _npcsPerHub = 1;
        [SerializeField] private int _facilitiesPerHub = 1;

        [Header("Presentation")]
        [SerializeField] private Vector2 _referenceSize = new Vector2(1280, 720);
        [SerializeField] private Vector2 _ringCenter = new Vector2(640, 365);
        [SerializeField] private Vector2 _ringRadius = new Vector2(405, 180);
        [SerializeField] private int _captureFrames = 90;
        private IReadOnlyList<HubDefinition> _definitions;
        private RunState _state;
        private int _stop;
        private int _lap;
        private GUIStyle _title;
        private GUIStyle _label;
        private GUIStyle _small;
        private GUIStyle _button;

        void Awake()
        {
            _definitions = HubData.Parse(_profiles.text);
            var population = new HubDemoPopulation();
            foreach (HubDefinition definition in _definitions)
                population.SetCounts(definition.Id, _npcsPerHub, _facilitiesPerHub);
            _state = new RunState(_definitions, population, population);
        }

        IEnumerator Start()
        {
            string evidence = Argument("--hub-evidence");
            if (evidence == null) yield break;
            Directory.CreateDirectory(evidence);
            RuinCurrent();
            for (int i = 0; i < _state.HubIds.Count; i++) NextStop();
            Debug.Log("Story004: completed ring and docked at ruined " + _state.HubIds[_stop]
                + "; nodes=" + _state.HubIds.Count + "; lost=" + _state.IsRunLost);
            for (int frame = 0; frame < _captureFrames; frame++) yield return null;
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(evidence, "01-ruined-hub.png"));
            for (int frame = 0; frame < _captureFrames; frame++) yield return null;
            foreach (HubDefinition definition in _definitions)
                _state.ApplyDelta(definition.Id, definition.Minimum - definition.Maximum, "Demo ruin scenario");
            Debug.Log("Story004: every hub ruined; lost=" + _state.IsRunLost + "; nodes=" + _state.HubIds.Count);
            for (int frame = 0; frame < _captureFrames; frame++) yield return null;
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(evidence, "02-run-lost.png"));
            for (int frame = 0; frame < _captureFrames; frame++) yield return null;
            Application.Quit();
        }

        void OnGUI()
        {
            EnsureStyles();
            GUI.matrix = Matrix4x4.Scale(new Vector3(Screen.width / _referenceSize.x, Screen.height / _referenceSize.y, 1));
            Fill(new Rect(0, 0, 1280, 720), new Color(0.055f, 0.075f, 0.09f));
            GUI.Label(new Rect(45, 25, 1100, 50), "IRON RING  /  HUB GROWTH", _title);
            GUI.Label(new Rect(47, 80, 1150, 35), "STORY 004     Permanent ring  /  Data-driven growth  /  Irreversible ruins", _small);
            DrawRing();
            for (int i = 0; i < _definitions.Count; i++) DrawHub(i);
            string current = _state.HubIds[_stop];
            GUI.Label(new Rect(430, 290, 420, 45), _state.IsRunLost ? "RUN LOST" : "RUN CONTINUES", _title);
            GUI.Label(new Rect(430, 340, 420, 65), _state.IsRunLost ? "Every hub has fallen.\nThe ring remains intact." : "LAP " + _lap + "  /  DOCKED AT " + current.ToUpperInvariant(), _label);
            GUI.Label(new Rect(430, 405, 420, 50), _state.IsRuined(current) ? "Train docked at a ruin" : "Train docked at a living hub", _small);
            GUI.enabled = !_state.IsRunLost;
            if (GUI.Button(new Rect(250, 620, 240, 48), "Depart / next stop", _button)) NextStop();
            if (GUI.Button(new Rect(520, 620, 240, 48), "Complete one lap", _button)) _state.OnLapCompleted(++_lap);
            if (GUI.Button(new Rect(790, 620, 240, 48), "Ruin current hub", _button)) RuinCurrent();
            GUI.enabled = true;
            GUI.Label(new Rect(100, 680, 1080, 28), "Demo controls simulate visits and damage. NPCs and facilities are local interface fakes.", _small);
        }

        /// <summary>Creates reusable text styles from the active GUI skin; presentation fields remain author-controlled.</summary>
        private void EnsureStyles()
        {
            if (_title != null) return;
            _title = new GUIStyle(GUI.skin.label) { fontSize = 30, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _label = new GUIStyle(GUI.skin.label) { fontSize = 20, alignment = TextAnchor.MiddleCenter };
            _small = new GUIStyle(GUI.skin.label) { fontSize = 16, alignment = TextAnchor.MiddleCenter };
            _button = new GUIStyle(GUI.skin.button) { fontSize = 19 };
        }

        /// <summary>Uses index and configured ring placement to return a permanent hub center in reference-screen coordinates.</summary>
        private Vector2 Position(int index)
        {
            float angle = Mathf.PI * 2 * index / _definitions.Count - Mathf.PI * 0.75f;
            return _ringCenter + Vector2.Scale(new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)), _ringRadius);
        }

        /// <summary>Draws both continuous rails through every hub position regardless of ruin state.</summary>
        private void DrawRing()
        {
            for (int i = 0; i < _definitions.Count; i++)
            {
                Vector2 from = Position(i);
                Vector2 to = Position((i + 1) % _definitions.Count);
                Vector2 direction = to - from;
                Matrix4x4 previous = GUI.matrix;
                GUIUtility.RotateAroundPivot(Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg, from);
                Fill(new Rect(from.x, from.y - 10, direction.magnitude, 4), new Color(0.65f, 0.52f, 0.31f));
                Fill(new Rect(from.x, from.y + 7, direction.magnitude, 4), new Color(0.65f, 0.52f, 0.31f));
                for (int tie = 0; tie < direction.magnitude; tie += 22)
                    Fill(new Rect(from.x + tie, from.y - 15, 3, 32), new Color(0.34f, 0.29f, 0.22f));
                GUI.matrix = previous;
            }
        }

        /// <summary>Draws index's standing buildings or broken rubble plus prosperity and the train's current dock marker.</summary>
        private void DrawHub(int index)
        {
            HubDefinition definition = _definitions[index];
            Vector2 center = Position(index);
            bool ruined = _state.IsRuined(definition.Id);
            Color brass = new Color(0.83f, 0.64f, 0.3f);
            Color stone = ruined ? new Color(0.47f, 0.42f, 0.4f) : brass;
            Fill(new Rect(center.x - 135, center.y - 92, 270, 184), new Color(0.105f, 0.13f, 0.15f));
            GUI.Label(new Rect(center.x - 130, center.y - 88, 260, 30), definition.Id.ToUpperInvariant() + (ruined ? "  /  RUINS" : "  /  ACTIVE"), _label);
            if (ruined)
            {
                Fill(new Rect(center.x - 70, center.y - 10, 28, 27), stone);
                Fill(new Rect(center.x - 31, center.y + 2, 46, 15), stone);
                Fill(new Rect(center.x + 28, center.y - 18, 22, 35), stone);
                Fill(new Rect(center.x + 59, center.y + 8, 25, 9), stone);
            }
            else
            {
                Fill(new Rect(center.x - 70, center.y - 40, 50, 57), stone);
                Fill(new Rect(center.x - 5, center.y - 28, 75, 45), stone);
                Fill(new Rect(center.x + 40, center.y - 50, 13, 22), stone);
                Fill(new Rect(center.x - 54, center.y - 27, 13, 17), Color.black);
                Fill(new Rect(center.x + 12, center.y - 16, 15, 15), Color.black);
            }
            float prosperity = _state.GetProsperity(definition.Id);
            GUI.Label(new Rect(center.x - 130, center.y + 20, 260, 30), "Prosperity " + prosperity.ToString("0.#") + " / " + definition.Maximum, _small);
            GUI.Label(new Rect(center.x - 130, center.y + 51, 260, 30), index == _stop ? "[ TRAIN DOCKED HERE ]" : "STOP REMAINS VISITABLE", _small);
        }

        /// <summary>Draws rect with color, restoring GUI tint afterward; changes only this frame's presentation.</summary>
        private static void Fill(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }

        /// <summary>Advances to the next retained hub, including ruins, and ticks growth after a full circuit.</summary>
        private void NextStop()
        {
            if (_state.IsRunLost) return;
            _stop = (_stop + 1) % _state.HubIds.Count;
            if (_stop == 0) _state.OnLapCompleted(++_lap);
        }

        /// <summary>Applies data-bounded demo damage to the current stop without changing the ring or its hub identity.</summary>
        private void RuinCurrent()
        {
            HubDefinition definition = _definitions[_stop];
            _state.ApplyDelta(definition.Id, definition.Minimum - definition.Maximum, "Demo damage");
        }

        /// <summary>Finds name in process arguments and returns its following value, or null when absent.</summary>
        private static string Argument(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++) if (args[i] == name) return args[i + 1];
            return null;
        }
    }
}
