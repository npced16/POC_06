using System.Collections.Generic;

using UnityEngine;

using IronRing.Contracts;
using IronRing.Contracts.Presentation;
using IronRing.Presentation.Effects;

namespace IronRing.Presentation.Interior
{
    /// <summary>
    /// 스토리 036. 거점 내부 폐허 연출. 씬의 HubInterior 인스턴스 루트(HubInteriorRoot)에 하나씩 붙고(RuinBuild.Generate), 거점 단계·칸 상태가 바뀔 때마다
    /// 빈 바닥 칸(Empty) 위 소품을 결정적으로 다시 계산한다: 칸마다 hash(거점 id, 칸) &lt; 단계 밀도면 가중치로 고른 종류를 RuinProp 프리팹으로 놓고,
    /// 열차 앵커에 가장 가까운 빈 칸 하나에 모닥불(연기 포함)을 둔다. 아이템·시설이 놓인 칸은 즉시 지우고 이번 판 동안 다시 놓지 않으며,
    /// 밀도가 내려가 빠지는 소품은 _fadeSeconds에 걸쳐 투명해진 뒤 사라진다. 콜라이더가 없어 클릭·고스트·판정에 끼지 않는다.
    /// </summary>
    [RequireComponent(typeof(HubInteriorRoot))]
    public sealed class RuinDressingView : MonoBehaviour
    {
        [SerializeField] private RuinCatalog _catalog;

        private HubInteriorRoot _root;
        private IHubInterior _interior;
        private IHubDevelopment _development;
        private InteriorCellView[] _cells;
        private readonly Dictionary<int, Prop> _props = new Dictionary<int, Prop>();
        private readonly HashSet<int> _cleared = new HashSet<int>();
        private readonly List<int> _done = new List<int>();
        private int _campfireCell = -2;
        private bool _bound;

        /// <summary>연결된 카탈로그.</summary>
        public RuinCatalog Catalog => _catalog;
        /// <summary>지금 보이는(사라지는 중이 아닌) 소품 수.</summary>
        public int VisibleCount
        {
            get
            {
                int count = 0;
                foreach (Prop p in _props.Values) if (p.Fade < 0f) count++;
                return count;
            }
        }

        /// <summary>편집기 제작 도구가 카탈로그를 연결한다.</summary>
        public void Configure(RuinCatalog catalog) => _catalog = catalog;

        private void OnEnable() => GameServices.Ready += Bind;

        private void OnDisable()
        {
            GameServices.Ready -= Bind;
            if (!_bound) return;
            if (_interior != null) _interior.CellChanged -= OnCellChanged;
            if (_development != null) _development.PhaseChanged -= OnPhaseChanged;
            _bound = false;
        }

        private void Bind()
        {
            if (_bound) return;
            _root = GetComponent<HubInteriorRoot>();
            _interior = GameServices.Get<IHubInterior>();
            _development = GameServices.Get<IHubDevelopment>();
            if (_interior == null || _catalog == null || _catalog.PropPrefab == null || _root == null || string.IsNullOrEmpty(_root.HubId)) return;
            IReadOnlyList<IInteriorCell> cells = _interior.Cells(_root.HubId);
            if (cells == null) return;
            _cells = new InteriorCellView[cells.Count];
            foreach (InteriorCellView view in _root.Cells())
                if (view.Index >= 0 && view.Index < _cells.Length) _cells[view.Index] = view;
            _interior.CellChanged += OnCellChanged;
            if (_development != null) _development.PhaseChanged += OnPhaseChanged;
            _bound = true;
            Refresh(true);
        }

        private void OnCellChanged(string hubId, int cellIndex) { if (_root != null && hubId == _root.HubId) Refresh(false); }

        private void OnPhaseChanged(string hubId, IronRing.Contracts.HubPhase phase) { if (_root != null && hubId == _root.HubId) Refresh(false); }

        /// <summary>현재 단계·칸 상태로 놓일 소품을 다시 계산한다. immediate면 빠지는 소품을 바로 지운다.</summary>
        private void Refresh(bool immediate)
        {
            string hubId = _root.HubId;
            IReadOnlyList<IInteriorCell> cells = _interior.Cells(hubId);
            RuinDressingData data = _catalog.Data;
            IronRing.Contracts.HubPhase phase = _development != null ? _development.GetPhase(hubId) : IronRing.Contracts.HubPhase.Ruined;
            float density = data.Density(phase);
            bool fire = data.CampfireIn(phase);
            int seed = Seed(hubId) ^ data._seedSalt;
            int count = Mathf.Min(cells.Count, _cells.Length);
            for (int i = 0; i < count; i++)
                if (cells[i].State == InteriorCellState.ItemPlaced) _cleared.Add(i);
            if (_campfireCell == -2) _campfireCell = PickCampfireCell(cells, count);

            for (int i = 0; i < count; i++)
            {
                bool empty = cells[i].State == InteriorCellState.Empty;
                string want = null;
                if (empty && !_cleared.Contains(i) && _cells[i] != null)
                {
                    if (i == _campfireCell) want = fire ? RuinCatalog.Campfire : null;
                    else if (Roll(seed, i, 1) < density) want = data.PickKind(Hash(seed, i, 2));
                }
                if (_props.TryGetValue(i, out Prop prop))
                {
                    if (want == null)
                    {
                        if (!empty || immediate || data._fadeSeconds <= 0f) Remove(i);
                        else if (prop.Fade < 0f) prop.Fade = 0f;
                    }
                    else if (prop.Fade >= 0f) { prop.Fade = -1f; prop.SetAlpha(1f); }
                }
                else if (want != null) Spawn(i, want, seed, data);
            }
        }

        /// <summary>열차 앵커에 가장 가까운 빈 칸(같으면 작은 색인). 없으면 -1.</summary>
        private int PickCampfireCell(IReadOnlyList<IInteriorCell> cells, int count)
        {
            Vector3 train = _root.TrainAnchor.position;
            int best = -1;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                if (cells[i].State != InteriorCellState.Empty || _cells[i] == null) continue;
                Vector3 d = _cells[i].transform.position - train;
                float distance = d.x * d.x + d.y * d.y;
                if (distance < bestDistance - 0.0001f) { bestDistance = distance; best = i; }
            }
            return best;
        }

        private void Spawn(int index, string kind, int seed, RuinDressingData data)
        {
            Sprite[] frames = _catalog.Frames(kind, Hash(seed, index, 3));
            if (frames == null || frames.Length == 0 || frames[0] == null) return;
            bool fire = kind == RuinCatalog.Campfire;
            float jx = fire ? 0f : (Roll(seed, index, 4) - 0.5f) * 2f * data._jitter;
            float jy = fire ? 0f : (Roll(seed, index, 5) - 0.5f) * 2f * data._jitter;
            GameObject go = Instantiate(_catalog.PropPrefab, _cells[index].transform.position + new Vector3(jx, jy, 0f), Quaternion.identity, transform);
            go.name = "Ruin_" + kind + "_" + index;
            go.transform.localScale = Vector3.one * (fire ? data._campfireScale : data._propScale);
            int order = (_root.Catalog != null ? _root.Catalog.CellOrder : 0) + data._sortingOffset;
            if (go.TryGetComponent(out SpriteRenderer renderer))
            {
                renderer.sprite = frames[0];
                renderer.sortingOrder = order;
                renderer.flipX = !fire && Hash(seed, index, 6) % 2 == 0;
            }
            if (go.TryGetComponent(out SpriteFrameLoop loop))
                loop.Configure(frames, frames.Length > 1 ? (fire ? data._campfireFramesPerSecond : data._framesPerSecond) : 0f);
            if (fire && _catalog.SmokePrefab != null)
            {
                GameObject smoke = Instantiate(_catalog.SmokePrefab, go.transform);
                smoke.transform.localPosition = new Vector3(0f, data._smokeOffsetY, 0f);
                smoke.transform.localScale = Vector3.one * data._smokeScale;
                foreach (SpriteRenderer r in smoke.GetComponentsInChildren<SpriteRenderer>(true)) r.sortingOrder = order + 1;
            }
            _props[index] = new Prop(go);
        }

        private void Remove(int index)
        {
            if (!_props.TryGetValue(index, out Prop prop)) return;
            if (prop.Go != null) Destroy(prop.Go);
            _props.Remove(index);
        }

        private void Update()
        {
            if (_props.Count == 0 || _catalog == null) return;
            float duration = Mathf.Max(0.01f, _catalog.Data._fadeSeconds);
            _done.Clear();
            foreach (KeyValuePair<int, Prop> pair in _props)
            {
                Prop prop = pair.Value;
                if (prop.Fade < 0f) continue;
                prop.Fade += Time.deltaTime;
                float alpha = 1f - prop.Fade / duration;
                if (alpha <= 0f) _done.Add(pair.Key);
                else prop.SetAlpha(alpha);
            }
            foreach (int index in _done) Remove(index);
        }

        /// <summary>거점 id의 FNV-1a 해시(실행마다 같다).</summary>
        private static int Seed(string hubId)
        {
            unchecked
            {
                int h = (int)2166136261;
                foreach (char c in hubId) { h ^= c; h *= 16777619; }
                return h;
            }
        }

        private static int Hash(int seed, int index, int salt)
        {
            unchecked
            {
                int h = seed ^ index * 73856093 ^ salt * 19349663;
                h ^= h >> 13; h *= 0x5bd1e995; h ^= h >> 15;
                return h & 0x7fffffff;
            }
        }

        private static float Roll(int seed, int index, int salt) => (Hash(seed, index, salt) & 0xffff) / 65536f;

        private sealed class Prop
        {
            public readonly GameObject Go;
            public float Fade = -1f;
            private readonly SpriteRenderer[] _renderers;
            private readonly float[] _alphas;

            public Prop(GameObject go)
            {
                Go = go;
                _renderers = go.GetComponentsInChildren<SpriteRenderer>(true);
                _alphas = new float[_renderers.Length];
                for (int i = 0; i < _renderers.Length; i++) _alphas[i] = _renderers[i].color.a;
            }

            public void SetAlpha(float t)
            {
                for (int i = 0; i < _renderers.Length; i++)
                {
                    if (_renderers[i] == null) continue;
                    Color c = _renderers[i].color;
                    c.a = _alphas[i] * t;
                    _renderers[i].color = c;
                }
            }
        }
    }
}
