using UnityEngine;

using IronRing.Contracts;
using IronRing.Contracts.Presentation;
using IronRing.Localization;
using IronRing.Presentation.Effects;
using IronRing.Presentation.Map;

using DevPhase = IronRing.Contracts.HubPhase;

namespace IronRing.Presentation.HubPhase
{
    /// <summary>
    /// Story 014: on the ring map, swaps each hub building sprite (under IMapAnchors.HubAnchor) between its normal and
    /// "_ruined" version by phase. Story 010 only swaps toward ruins; this view also restores the normal art when a hub
    /// recovers. MapView is not edited.
    /// </summary>
    public sealed class RingHubPhaseView : MonoBehaviour
    {
        private const string RuinedSuffix = "_ruined";
        private const string SmokeName = "Smoke";
        private static RingHubPhaseView _instance;
        private EffectsSettings _art;
        private IMapAnchors _anchors;
        private IHubStateSystem _hubs;
        private IHubDevelopment _development;

        /// <summary>Clears statics for a new play session (domain reload disabled).</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { GameServices.Ready -= Create; _instance = null; }

        /// <summary>Waits for GameServices.Ready after the scene loads.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot() { GameServices.Ready -= Create; GameServices.Ready += Create; }

        /// <summary>Creates the view once when map anchors, hub state and development are registered.</summary>
        private static void Create()
        {
            if (_instance != null) return;
            IMapAnchors anchors = GameServices.Get<IMapAnchors>();
            IHubStateSystem hubs = GameServices.Get<IHubStateSystem>();
            IHubDevelopment development = GameServices.Get<IHubDevelopment>();
            if (anchors == null || hubs == null || development == null) return;
            _instance = new GameObject(nameof(RingHubPhaseView)).AddComponent<RingHubPhaseView>();
            _instance.Initialize(anchors, hubs, development);
        }

        /// <summary>Stores injected systems, applies every hub's current phase and subscribes to phase changes.</summary>
        private void Initialize(IMapAnchors anchors, IHubStateSystem hubs, IHubDevelopment development)
        {
            _art = HubPhaseSprites.LoadArt(HubPhaseViewSettings.Load());
            _anchors = anchors; _hubs = hubs; _development = development;
            foreach (string hubId in hubs.HubIds) Apply(hubId);
            _development.PhaseChanged += OnPhaseChanged;
        }

        private void OnPhaseChanged(string hubId, DevPhase phase) => Apply(hubId);

        /// <summary>Sets every hub-anchor sprite of hubId to its ruined art when Ruined/Collapsed, otherwise to its normal art, and toggles the chimney smoke.</summary>
        private void Apply(string hubId)
        {
            if (_art == null) return;
            Transform anchor = _anchors.HubAnchor(hubId);
            if (anchor == null) return;
            DevPhase phase = _development.GetPhase(hubId);
            bool ruined = phase == DevPhase.Ruined || phase == DevPhase.Collapsed;
            foreach (SpriteRenderer renderer in anchor.GetComponentsInChildren<SpriteRenderer>())
            {
                if (renderer.sprite == null) continue;
                string name = renderer.sprite.name;
                string normal = name.EndsWith(RuinedSuffix) ? name.Substring(0, name.Length - RuinedSuffix.Length) : name;
                Sprite target = _art.Sprite(ruined ? normal + RuinedSuffix : normal);
                if (target != null && target != renderer.sprite) renderer.sprite = target;
            }
            // Story 019: chimney smoke (prefab child "Smoke") only for Recovering/Settled hubs.
            foreach (Transform child in anchor.GetComponentsInChildren<Transform>(true))
                if (child.name == SmokeName && child.gameObject.activeSelf == ruined) child.gameObject.SetActive(!ruined);
            // Story 022: world-space TMP labels on the HubBuilding prefab (name + phase), replacing PhaseHud's UI Toolkit map labels.
            MapLabel label = anchor.GetComponentInChildren<MapLabel>(true);
            if (label != null)
            {
                label.SetName(Strings.Get(hubId.Replace('-', '_')));
                label.SetPhase(phase, PhaseText(phase));
            }
        }

        /// <summary>Korean phase name for phase (same keys as the phase banner).</summary>
        private static string PhaseText(DevPhase phase)
        {
            switch (phase)
            {
                case DevPhase.Recovering: return Strings.Get("phase_recovering");
                case DevPhase.Settled: return Strings.Get("phase_settled");
                case DevPhase.Collapsed: return Strings.Get("phase_collapsed");
                default: return Strings.Get("phase_ruined");
            }
        }

        void OnDestroy()
        {
            if (_development != null) _development.PhaseChanged -= OnPhaseChanged;
            if (_instance == this) _instance = null;
        }
    }
}
