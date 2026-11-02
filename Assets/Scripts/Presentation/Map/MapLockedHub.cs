using System;

using UnityEngine;

using IronRing.Contracts;
using IronRing.Contracts.Presentation;

namespace IronRing.Presentation.Map
{
    /// <summary>
    /// 스토리 048. 잠긴 역(ring-track.json `_locked`)의 지도 표지. BuildTopDownMain이 잠긴 역의 MapNode에 붙인다.
    /// 잠겨 있으면 역 건물을 어두운 폐허 실루엣(_lockedTint)으로, 라벨을 "폐쇄된 선로"로, 클릭(HubTarget 콜라이더)과 둘레 폐허 소품을 끈다.
    /// ILineOpener.HubOpened가 오면 원래 색·라벨 "{이름} · 폐허"로 바꾸고 클릭과 폐허 소품(MapRuinProps, 단계별 켜짐은 그쪽 규칙)을 켠다.
    /// </summary>
    public sealed class MapLockedHub : MonoBehaviour
    {
        [SerializeField] private string _hubId;
        [SerializeField] private SpriteRenderer[] _renderers = Array.Empty<SpriteRenderer>();
        [SerializeField] private TextMesh _label;
        [SerializeField] private Collider2D[] _colliders = Array.Empty<Collider2D>();
        [SerializeField] private string _lockedText;
        [SerializeField] private string _openText;
        [SerializeField] private Color _lockedTint = new Color(0.22f, 0.2f, 0.24f, 1f);
        private Color[] _openColors;
        private ILineOpener _opener;
        private bool? _applied;

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
            _openColors = new Color[_renderers.Length];
            for (int i = 0; i < _renderers.Length; i++) _openColors[i] = _renderers[i] != null ? _renderers[i].color : Color.white;
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
            Apply(_opener.IsOpen(_hubId));
        }

        private void OnOpened(string hubId) { if (hubId == _hubId) Apply(true); }

        private void LateUpdate()
        {
            // 폐허 소품은 RuinBuild.GenerateMap이 따로 만들고 단계마다 켜고 끈다. 잠긴 동안은 렌더러만 꺼서 그 규칙과 겹치지 않게 한다.
            if (_applied != false) return;
            SetRuinProps(false);
            SetMapLabel(false);
        }

        private void Apply(bool open)
        {
            if (_applied == open) return;
            _applied = open;
            for (int i = 0; i < _renderers.Length; i++)
                if (_renderers[i] != null) _renderers[i].color = open ? _openColors[i] : _lockedTint;
            foreach (Collider2D c in _colliders) if (c != null) c.enabled = open;
            if (_label != null) _label.text = open ? _openText : _lockedText;
            SetRuinProps(open);
            SetMapLabel(open);
        }

        /// <summary>보이는 이름은 HubBuilding 프리팹의 MapLabel(TMP)이다. 잠긴 동안 "폐쇄된 선로"로 덮고 단계 줄을 숨긴다. 열리면 한 번 "{이름} · 폐허".</summary>
        private void SetMapLabel(bool open)
        {
            MapLabel label = GetComponentInChildren<MapLabel>(true);
            if (label == null) return;
            label.SetPhaseVisible(open);
            if (!open) label.SetName(_lockedText);
            else label.SetName(_openText);
        }

        private void SetRuinProps(bool visible)
        {
            foreach (MapRuinProps props in FindObjectsByType<MapRuinProps>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (props.HubId != _hubId) continue;
                foreach (Renderer r in props.GetComponentsInChildren<Renderer>(true)) if (r.enabled != visible) r.enabled = visible;
            }
        }
    }
}
