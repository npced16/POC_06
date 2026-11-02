using System;
using System.Collections.Generic;
using System.Linq;

using UnityEngine;

using IronRing.Contracts;
using IronRing.Contracts.Presentation;
using IronRing.Localization;

namespace IronRing.Presentation.Map
{
    /// <summary>
    /// 스토리 048/049. 잠긴 역(ring-track.json `_locked`)의 지도 표지. BuildTopDownMain이 잠긴 역의 MapNode에 붙인다.
    /// 잠겨 있으면 역 건물·라벨, 둘레 폐허 소품(MapRuinProps), 거점 장식 소품(씬 Props/Prop_{거점 순번}_*)을 아예 그리지 않고 클릭도 끈다 — 선로만 지나간다.
    /// ILineOpener.HubOpened가 오면 씬에 저장된 켜짐 상태로 되돌리고 _revealSeconds 동안 투명 → 불투명으로 폐허가 나타난다.
    /// </summary>
    public sealed class MapLockedHub : MonoBehaviour
    {
        [SerializeField] private string _hubId;
        [SerializeField] private SpriteRenderer[] _renderers = Array.Empty<SpriteRenderer>();
        [SerializeField] private TextMesh _label;
        [SerializeField] private Collider2D[] _colliders = Array.Empty<Collider2D>();
        [SerializeField] private string _lockedText;
        [SerializeField] private string _openText;
        [SerializeField, Min(0f)] private float _revealSeconds = 1.5f;
        private ILineOpener _opener;
        private bool? _applied;
        private float _reveal = -1f;
        private readonly List<Renderer> _managed = new List<Renderer>();
        private readonly Dictionary<Renderer, bool> _authored = new Dictionary<Renderer, bool>();
        private readonly Dictionary<UnityEngine.Object, float> _baseAlpha = new Dictionary<UnityEngine.Object, float>();

        /// <summary>이 표지의 거점 id.</summary>
        public string HubId => _hubId;

        /// <summary>편집기 제작 도구가 거점 id, 건물 렌더러, 라벨, 클릭 콜라이더, 두 라벨 문구를 기록한다.</summary>
        public void Configure(string hubId, SpriteRenderer[] renderers, TextMesh label, Collider2D[] colliders, string lockedText, string openText)
        {
            _hubId = hubId;
            _renderers = renderers ?? Array.Empty<SpriteRenderer>();
            _label = label;
            _colliders = colliders ?? Array.Empty<Collider2D>();
            _lockedText = lockedText;
            _openText = openText;
        }

        private void Awake()
        {
            Collect();
            Apply(false);
        }

        private void OnEnable() => GameServices.Ready += Bind;

        private void OnDisable()
        {
            GameServices.Ready -= Bind;
            if (_opener != null) _opener.HubOpened -= OnOpened;
            _opener = null;
        }

        private void Start() => Bind();

        private void Bind()
        {
            if (_opener != null) return;
            _opener = GameServices.Get<ILineOpener>();
            if (_opener == null) return;
            _opener.HubOpened += OnOpened;
            if (_opener.IsOpen(_hubId)) { Apply(true); SetAlpha(1f); }
        }

        private void OnOpened(string hubId)
        {
            if (hubId != _hubId) return;
            Apply(true);
            _reveal = 0f;
            SetAlpha(0f);
        }

        private void LateUpdate()
        {
            // 다른 뷰(RingHubPhaseView, MapRuinProps 단계 규칙)가 렌더러를 다시 켜도 잠긴 동안은 매 프레임 끈다.
            if (_applied == false) { Hide(); return; }
            if (_reveal < 0f) return;
            _reveal += Time.deltaTime;
            float t = _revealSeconds <= 0f ? 1f : Mathf.Clamp01(_reveal / _revealSeconds);
            SetAlpha(t * t * (3f - 2f * t));
            if (t >= 1f) _reveal = -1f;
        }

        /// <summary>숨길 렌더러(역 아래 전부, 폐허 소품, 장식 소품)와 씬에 저장된 켜짐 상태를 기억한다.</summary>
        private void Collect()
        {
            var all = new List<Renderer>(GetComponentsInChildren<Renderer>(true));
            foreach (MapRuinProps props in FindObjectsByType<MapRuinProps>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (props.HubId == _hubId) all.AddRange(props.GetComponentsInChildren<Renderer>(true));
            string prefix = DecorPrefix();
            if (prefix != null)
                foreach (GameObject root in gameObject.scene.GetRootGameObjects())
                {
                    if (root.name != "Props") continue;
                    foreach (Transform child in root.transform)
                        if (child.name.StartsWith(prefix, StringComparison.Ordinal)) all.AddRange(child.GetComponentsInChildren<Renderer>(true));
                }
            foreach (Renderer r in all)
            {
                if (r == null || _authored.ContainsKey(r)) continue;
                _authored[r] = r.enabled;
                _managed.Add(r);
            }
        }

        /// <summary>PropsBuild가 거점 id 순번(1부터)으로 붙인 장식 소품 이름 머리 "Prop_{n}_".</summary>
        private string DecorPrefix()
        {
            string[] ids = FindObjectsByType<HubTarget>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Select(h => h.HubId).Distinct().OrderBy(id => id, StringComparer.Ordinal).ToArray();
            int index = Array.IndexOf(ids, _hubId);
            return index < 0 ? null : "Prop_" + (index + 1) + "_";
        }

        private void Apply(bool open)
        {
            if (_applied == open) return;
            _applied = open;
            foreach (Collider2D c in _colliders) if (c != null) c.enabled = open;
            if (_label != null) _label.text = open ? _openText : _lockedText;
            if (!open) { Hide(); return; }
            foreach (Renderer r in _managed) if (r != null) r.enabled = _authored[r];
            MapLabel label = GetComponentInChildren<MapLabel>(true);
            if (label != null) { label.SetPhaseVisible(true); label.SetName(Strings.Get(_hubId.Replace('-', '_'))); }
        }

        private void Hide()
        {
            foreach (Renderer r in _managed) if (r != null && r.enabled) r.enabled = false;
        }

        /// <summary>나타나는 동안 스프라이트·TMP 알파를 원래 알파 × a로 둔다.</summary>
        private void SetAlpha(float a)
        {
            foreach (Renderer r in _managed)
            {
                if (r is SpriteRenderer sr)
                {
                    if (!_baseAlpha.TryGetValue(sr, out float b)) _baseAlpha[sr] = b = sr.color.a;
                    Color c = sr.color; c.a = b * a; sr.color = c;
                }
                else if (r != null && r.TryGetComponent(out TMPro.TMP_Text t))
                {
                    if (!_baseAlpha.TryGetValue(t, out float b)) _baseAlpha[t] = b = t.alpha;
                    t.alpha = b * a;
                }
            }
        }
    }
}
