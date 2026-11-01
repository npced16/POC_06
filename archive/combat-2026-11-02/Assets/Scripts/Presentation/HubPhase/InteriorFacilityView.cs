using System.Collections.Generic;

using UnityEngine;

using IronRing.Contracts;
using IronRing.Contracts.Presentation;
using IronRing.Hub;

namespace IronRing.Presentation.HubPhase
{
    /// <summary>
    /// Story 014: while the interior view is open, shows a facility sprite on every built plot cell of that hub
    /// (IInteriorFacilityPlots records first, then IHubInterior FacilityBuilt cells).
    /// </summary>
    public sealed class InteriorFacilityView : MonoBehaviour
    {
        private static InteriorFacilityView _instance;
        private static bool _loggedMissing;
        private HubPhaseViewSettings _settings;
        private HubPhasePrefabCatalog _catalog;
        private IViewMode _view;
        private IInteriorAnchors _anchors;
        private IHubInterior _interior;
        private IInteriorFacilityPlots _plots;
        private readonly List<GameObject> _sprites = new List<GameObject>();

        /// <summary>Clears statics for a new play session (domain reload disabled).</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { GameServices.Ready -= Create; _instance = null; _loggedMissing = false; }

        /// <summary>Waits for GameServices.Ready after the scene loads.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot() { GameServices.Ready -= Create; GameServices.Ready += Create; }

        /// <summary>Creates the view once when the view mode, anchors, interior and plots are registered.</summary>
        private static void Create()
        {
            if (_instance != null) return;
            IViewMode view = GameServices.Get<IViewMode>();
            IInteriorAnchors anchors = GameServices.Get<IInteriorAnchors>();
            IHubInterior interior = GameServices.Get<IHubInterior>();
            IInteriorFacilityPlots plots = GameServices.Get<IInteriorFacilityPlots>();
            if (view == null || anchors == null || interior == null || plots == null)
            {
                if (!_loggedMissing) UnityEngine.Debug.Log("InteriorFacilityView: 내부 화면 서비스 미등록 - 내부 시설 표시를 건너뜁니다.");
                _loggedMissing = true;
                return;
            }
            _instance = new GameObject(nameof(InteriorFacilityView)).AddComponent<InteriorFacilityView>();
            _instance.Initialize(view, anchors, interior, plots);
        }

        /// <summary>Stores injected systems, subscribes to changes and draws the current state.</summary>
        private void Initialize(IViewMode view, IInteriorAnchors anchors, IHubInterior interior, IInteriorFacilityPlots plots)
        {
            _settings = HubPhaseViewSettings.Load();
            _catalog = HubPhasePrefabCatalog.Load();
            _view = view; _anchors = anchors; _interior = interior; _plots = plots;
            _view.InteriorChanged += OnInteriorChanged;
            _interior.CellChanged += OnCellChanged;
            _plots.FacilityPlaced += OnFacilityPlaced;
            Rebuild();
        }

        private void OnInteriorChanged(bool open) => Rebuild();
        private void OnCellChanged(string hubId, int cellIndex) { if (hubId == _view.InteriorHubId) Rebuild(); }
        private void OnFacilityPlaced(string hubId, int cellIndex, string facilityId) { if (hubId == _view.InteriorHubId) Rebuild(); }

        /// <summary>Redraws facility sprites for the open interior's hub, or clears them when the interior is closed.</summary>
        private void Rebuild()
        {
            foreach (GameObject sprite in _sprites) if (sprite != null) Destroy(sprite);
            _sprites.Clear();
            string hub = _view.InteriorOpen ? _view.InteriorHubId : null;
            if (hub == null) return;
            IReadOnlyList<IInteriorCell> cells = _interior.Cells(hub);
            if (cells == null) return;
            float width = HubPhaseSprites.CellSpacing(_anchors, hub) * _settings.FacilityCellFraction;
            foreach (IInteriorCell cell in cells)
            {
                string facilityId = _plots.FacilityAt(hub, cell.Index);
                Transform anchor = facilityId != null ? _anchors.CellAnchor(hub, cell.Index) : null;
                if (anchor == null) continue;
                SpriteRenderer renderer = HubPhaseSprites.Spawn(_catalog != null ? _catalog.Facility(facilityId) : null, transform, anchor.position, "facility-" + facilityId);
                if (renderer == null) { UnityEngine.Debug.LogWarning("InteriorFacilityView: 프리팹 없음 " + facilityId); continue; }
                HubPhaseSprites.FitWidth(renderer, width);
                _sprites.Add(renderer.gameObject);
            }
        }

        void OnDestroy()
        {
            if (_view != null) _view.InteriorChanged -= OnInteriorChanged;
            if (_interior != null) _interior.CellChanged -= OnCellChanged;
            if (_plots != null) _plots.FacilityPlaced -= OnFacilityPlaced;
            if (_instance == this) _instance = null;
        }
    }
}
