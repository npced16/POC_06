using System;

using UnityEngine;

using IronRing.Contracts.Presentation;
using IronRing.Presentation.Effects;

namespace IronRing.Presentation.HubPhase
{
    /// <summary>
    /// Story 014 presentation tuning, wire format of Resources/HubPhase/hub_phase_view.json.
    /// _npcSpeed: world units per second; _npcPauseMin/_npcPauseMax: seconds an NPC idles at a cell;
    /// _npcCellFraction: sprite width as a fraction of interior cell spacing;
    /// _npcSortingOrder: SpriteRenderer order above the interior grid; _npcSeed: wander seed;
    /// _mapLabelOffsetY: fallback ring-map label offset below the hub anchor (world units) when the hub has no name label;
    /// _mapLabelGapPixels: panel-pixel gap between a hub's name label and its phase label; 
    /// _effectsSettings: Resources name of the sprite set;
    /// _npcHighlightColor: Story 017 sprite tint of the NPC whose dialog panel is open;
    /// _npcHoverOutlineColor, _npcHoverOutlinePixels: outline colour and thickness (sprite pixels) of the NPC under the mouse.
    /// </summary>
    [Serializable]
    public sealed class HubPhaseViewSettings
    {
        [SerializeField] private float _npcSpeed = 0.6f;
        [SerializeField] private float _npcPauseMin = 0.6f;
        [SerializeField] private float _npcPauseMax = 2f;
        [SerializeField] private float _npcCellFraction = 0.55f;
        [SerializeField] private int _npcSortingOrder = 60;
        [SerializeField] private int _npcSeed = 14;
        [SerializeField] private float _mapLabelOffsetY = -1.45f;
        [SerializeField] private float _mapLabelGapPixels = 4f;
        [SerializeField] private string _effectsSettings = "Story010Effects";
        [SerializeField] private bool _buildMenuOnCellClick = false;
        [SerializeField] private Color _npcHighlightColor = new Color(1f, 0.86f, 0.55f, 1f);
        [Tooltip("Story 023: walk speed from the train car to the home cell (world units/s).")]
        [SerializeField] private float _arrivalSpeed = 0.9f;
        [Tooltip("Story 023: pause at home after the arrival walk (s).")]
        [SerializeField] private float _arrivalPause = 0.8f;
        [Tooltip("Story 023: delay between NPCs leaving the train in the same docking (s).")]
        [SerializeField] private float _arrivalStagger = 1f;
        [Tooltip("스토리 027: false면 건물이 선 집의 NPC는 배회 대신 문 앞에 선다(건물 없는 NPC는 배회).")]
        [SerializeField] private bool _npcWanders = true;
        [Tooltip("스토리 027: 문 앞 위치 = 문 칸 중심에서의 세로 오프셋(월드).")]
        [SerializeField] private float _doorOffsetY = -0.42f;
        [SerializeField] private float _breatheAmplitude = 0.03f;
        [SerializeField] private float _breatheSpeed = 2.4f;
        [SerializeField] private float _emptyBuildingToastSeconds = 2.5f;
        [Tooltip("스토리 029: 한 자리(문 앞·집 칸 가운데·열차 옆)에 여럿이면 옆으로 벌리는 간격(칸 간격의 비율).")]
        [SerializeField] private float _npcPostSpacing = 0.45f;
        [Tooltip("스토리 029: 한 줄에 서는 최대 인원. 넘치면 뒤 줄.")]
        [SerializeField] private int _npcPostsPerRow = 3;
        [Tooltip("스토리 029: 뒤 줄이 앞 줄보다 위(뒤)로 물러나는 거리(칸 간격의 비율).")]
        [SerializeField] private float _npcRowOffset = 0.3f;
        [Tooltip("마우스가 올라간 NPC의 윤곽선 색.")]
        [SerializeField] private Color _npcHoverOutlineColor = new Color(0.25f, 1f, 0.35f, 1f);
        [Tooltip("마우스가 올라간 NPC의 윤곽선 두께(스프라이트 픽셀).")]
        [SerializeField] private float _npcHoverOutlinePixels = 2f;

        public float NpcSpeed => _npcSpeed;
        public float NpcPauseMin => _npcPauseMin;
        public float NpcPauseMax => Mathf.Max(_npcPauseMin, _npcPauseMax);
        public float NpcCellFraction => _npcCellFraction;
        public int NpcSortingOrder => _npcSortingOrder;
        public int NpcSeed => _npcSeed;
        public float MapLabelOffsetY => _mapLabelOffsetY;
        public float MapLabelGapPixels => _mapLabelGapPixels;
        public string EffectsSettingsName => _effectsSettings;
        /// <summary>Story 015 이후 시설은 핫바로 놓으므로 칸 클릭 건설 메뉴는 기본 꺼짐(데이터).</summary>
        public bool BuildMenuOnCellClick => _buildMenuOnCellClick;
        /// <summary>Story 017: tint of the NPC whose dialog panel is open (data).</summary>
        public Color NpcHighlightColor => _npcHighlightColor;
        /// <summary>Story 023: arrival walk speed.</summary>
        public float ArrivalSpeed => Mathf.Max(0.01f, _arrivalSpeed);
        /// <summary>Story 023: pause after arriving.</summary>
        public float ArrivalPause => Mathf.Max(0f, _arrivalPause);
        /// <summary>Story 023: delay between successive arrivals.</summary>
        public float ArrivalStagger => Mathf.Max(0f, _arrivalStagger);
        /// <summary>스토리 027: NPC 배회 여부(false = 문 앞 대기).</summary>
        public bool NpcWanders => _npcWanders;
        /// <summary>Story 029: sideways spacing between NPCs sharing one post (fraction of the cell spacing).</summary>
        public float NpcPostSpacing => _npcPostSpacing;
        /// <summary>Story 029: NPCs per row at one post (at least 1).</summary>
        public int NpcPostsPerRow => Mathf.Max(1, _npcPostsPerRow);
        /// <summary>Story 029: how far each further row stands behind (fraction of the cell spacing, world up).</summary>
        public float NpcRowOffset => _npcRowOffset;
        /// <summary>스토리 027: 문 앞 세로 오프셋.</summary>
        public float DoorOffsetY => _doorOffsetY;
        /// <summary>스토리 027: 문 앞 숨쉬기 흔들림 진폭(월드).</summary>
        public float BreatheAmplitude => Mathf.Max(0f, _breatheAmplitude);
        /// <summary>스토리 027: 숨쉬기 각속도.</summary>
        public float BreatheSpeed => _breatheSpeed;
        /// <summary>스토리 027: 빈 건물 안내 토스트 시간(초).</summary>
        public float EmptyBuildingToastSeconds => Mathf.Max(0.5f, _emptyBuildingToastSeconds);
        /// <summary>마우스가 올라간 NPC의 윤곽선 색.</summary>
        public Color NpcHoverOutlineColor => _npcHoverOutlineColor;
        /// <summary>마우스가 올라간 NPC의 윤곽선 두께(스프라이트 픽셀).</summary>
        public float NpcHoverOutlinePixels => Mathf.Max(0f, _npcHoverOutlinePixels);

        /// <summary>Loads the JSON tuning; logs and falls back to the serialized defaults when missing.</summary>
        public static HubPhaseViewSettings Load()
        {
            TextAsset asset = UnityEngine.Resources.Load<TextAsset>("HubPhase/hub_phase_view");
            if (asset == null)
            {
                UnityEngine.Debug.LogError("HubPhase: Resources/HubPhase/hub_phase_view.json를 찾지 못해 기본값을 씁니다.");
                return new HubPhaseViewSettings();
            }
            return JsonUtility.FromJson<HubPhaseViewSettings>(asset.text);
        }
    }

    /// <summary>Shared helpers for Story 014 views: prefab spawning (HubPhasePrefabCatalog) and Story 010's sprite set for map swaps.</summary>
    public static class HubPhaseSprites
    {
        /// <summary>Loads the Story 010 EffectsSettings named in settings, or null with an error log.</summary>
        public static EffectsSettings LoadArt(HubPhaseViewSettings settings)
        {
            EffectsSettings art = UnityEngine.Resources.Load<EffectsSettings>(settings.EffectsSettingsName);
            if (art == null) UnityEngine.Debug.LogError("HubPhase: 스프라이트 설정 " + settings.EffectsSettingsName + "을(를) 찾지 못했습니다.");
            return art;
        }

        /// <summary>Instantiates prefab under parent at position and returns its SpriteRenderer (null when the prefab is missing).</summary>
        public static SpriteRenderer Spawn(GameObject prefab, Transform parent, Vector3 position, string name)
        {
            if (prefab == null) return null;
            GameObject piece = UnityEngine.Object.Instantiate(prefab, position, Quaternion.identity, parent);
            piece.name = name;
            return piece.GetComponent<SpriteRenderer>();
        }

        /// <summary>Distance between the first two interior cell anchors of hubId, or 1 when unavailable.</summary>
        public static float CellSpacing(IInteriorAnchors anchors, string hubId)
        {
            Transform a = anchors.CellAnchor(hubId, 0);
            Transform b = anchors.CellAnchor(hubId, 1);
            float spacing = a != null && b != null ? Vector3.Distance(a.position, b.position) : 0f;
            return spacing > 0.0001f ? spacing : 1f;
        }

        /// <summary>Scales renderer uniformly so its sprite width equals width world units.</summary>
        public static void FitWidth(SpriteRenderer renderer, float width)
        {
            if (renderer.sprite == null) return;
            float size = renderer.sprite.bounds.size.x;
            if (size > 0.0001f) renderer.transform.localScale = Vector3.one * (width / size);
        }
    }
}
