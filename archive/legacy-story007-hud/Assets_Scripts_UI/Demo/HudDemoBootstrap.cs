using System;

using UnityEngine;
using UnityEngine.InputSystem;

namespace IronRing.UI.Demo
{
    /// <summary>Scene-local demo composition; never auto-installs into another story's scene.</summary>
    public sealed class HudDemoBootstrap : MonoBehaviour
    {
        [Header("Authored demo")]
        [SerializeField] private TextAsset _data;
        [SerializeField] private HudDocument _view;
        private HudController _model;
        private HudDemoSystems _systems;

        /// <summary>Receives authored data and view references while creating the demo asset.</summary>
        public void Configure(TextAsset data, HudDocument view) { _data = data; _view = view; }
        void Start()
        {
            var data = JsonUtility.FromJson<HudDemoData>(_data.text);
            _systems = new HudDemoSystems(data);
            _model = new HudController(_systems, _systems, _systems, _systems, _systems, _systems, _systems);
            _view.Bind(_model, data.Slots, data.FacilityIds);
            string[] args = Environment.GetCommandLineArgs();
            if (Array.IndexOf(args, "--ruined") >= 0) RuinOne();
            if (Array.IndexOf(args, "--defeat") >= 0) LoseRun();
            UnityEngine.Debug.Log("STORY007 READY hubs=" + _model.Hubs.Count + " resources=" + _model.Resources.Count);
        }
        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (keyboard.rKey.wasPressedThisFrame) RuinOne();
            if (keyboard.lKey.wasPressedThisFrame) LoseRun();
            if (keyboard.nKey.wasPressedThisFrame) _systems.Depart();
            if (keyboard.pKey.wasPressedThisFrame) _systems.OnLapCompleted(_systems.Lap + 1);
            if (keyboard.f12Key.wasPressedThisFrame)
            {
                string path = System.IO.Path.GetFullPath("production/qa/evidence/story-007-minimal-hud/04-mouse-actions.png");
                ScreenCapture.CaptureScreenshot(path);
            }
        }
        /// <summary>Uses the first authored hub and its current prosperity to emit one ruin transition.</summary>
        private void RuinOne() { string id = _systems.HubIds[0]; _systems.ApplyDelta(id, -_systems.GetProsperity(id), "Demo ruin"); }
        /// <summary>Uses every authored hub to emit ruin transitions and the run-lost event.</summary>
        private void LoseRun() { foreach (string id in _systems.HubIds) _systems.ApplyDelta(id, -_systems.GetProsperity(id), "Demo defeat"); }
        void OnDestroy() { _model?.Dispose(); }
    }
}
