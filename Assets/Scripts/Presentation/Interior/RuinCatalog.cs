using System;
using System.Collections.Generic;

using UnityEngine;

using IronRing.Contracts;

namespace IronRing.Presentation.Interior
{
    /// <summary>
    /// 스토리 036. 폐허 연출 카탈로그(Assets/Prefabs/Interior/RuinCatalog.asset). 종류(puddle·weeds·rubble·campfire·tent)별 변형 스프라이트 프레임,
    /// 소품 프리팹(RuinProp.prefab), 모닥불 연기 프리팹(Fx/Smoke.prefab)을 가진다. 수치(단계별 밀도·가중치·사라지는 시간·정렬)는
    /// Resources/Interior/RuinDressing.json에서 읽는다. 편집기 RuinBuild.Generate가 채운다.
    /// </summary>
    [CreateAssetMenu(menuName = "IronRing/Interior/Ruin Catalog", fileName = "RuinCatalog")]
    public sealed class RuinCatalog : ScriptableObject
    {
        /// <summary>모닥불 종류 id(거점당 1개, 열차 근처).</summary>
        public const string Campfire = "campfire";

        /// <summary>종류 하나의 변형 하나: 프레임 1장(정지) 또는 여러 장(루프).</summary>
        [Serializable]
        public sealed class Variant
        {
            [SerializeField] private string _kind;
            [SerializeField] private Sprite[] _frames;

            /// <summary>kind 종류의 frames 변형을 만든다.</summary>
            public Variant(string kind, Sprite[] frames) { _kind = kind; _frames = frames; }
            /// <summary>종류 id.</summary>
            public string Kind => _kind;
            /// <summary>프레임 스프라이트(첫 장이 정지 그림).</summary>
            public Sprite[] Frames => _frames;
        }

        [SerializeField] private Variant[] _variants = Array.Empty<Variant>();
        [SerializeField] private GameObject _propPrefab;
        [SerializeField] private GameObject _smokePrefab;
        [Tooltip("Resources 경로(확장자 없음).")]
        [SerializeField] private string _dataResource = RuinDressingData.Resource;
        [NonSerialized] private RuinDressingData _data;

        /// <summary>소품 프리팹(SpriteRenderer + SpriteFrameLoop, 콜라이더 없음).</summary>
        public GameObject PropPrefab => _propPrefab;
        /// <summary>모닥불 위 연기 프리팹. 없으면 연기 없이 둔다.</summary>
        public GameObject SmokePrefab => _smokePrefab;
        /// <summary>RuinDressing.json 수치(처음 읽을 때 한 번 불러온다).</summary>
        public RuinDressingData Data => _data ??= RuinDressingData.Load(_dataResource);

        /// <summary>kind 종류의 변형 수.</summary>
        public int VariantCount(string kind)
        {
            int count = 0;
            foreach (Variant v in _variants) if (v != null && v.Kind == kind) count++;
            return count;
        }

        /// <summary>kind 종류의 pick번째(나머지 연산) 변형 프레임. 없으면 null.</summary>
        public Sprite[] Frames(string kind, int pick)
        {
            int count = VariantCount(kind);
            if (count == 0) return null;
            int target = Mathf.Abs(pick) % count;
            foreach (Variant v in _variants)
            {
                if (v == null || v.Kind != kind) continue;
                if (target-- == 0) return v.Frames;
            }
            return null;
        }

        /// <summary>편집기 제작 도구가 변형, 소품 프리팹, 연기 프리팹을 기록한다.</summary>
        public void Configure(Variant[] variants, GameObject propPrefab, GameObject smokePrefab)
        {
            _variants = variants ?? Array.Empty<Variant>();
            _propPrefab = propPrefab;
            _smokePrefab = smokePrefab;
            _data = null;
        }
    }

    /// <summary>스토리 036. Resources/Interior/RuinDressing.json. 폐허 연출의 모든 수치(코드에 두지 않는다).</summary>
    [Serializable]
    public sealed class RuinDressingData
    {
        /// <summary>기본 Resources 경로.</summary>
        public const string Resource = "Interior/RuinDressing";

        /// <summary>단계 이름(IronRing.Contracts.HubPhase)과 값.</summary>
        [Serializable] public sealed class PhaseValue { public string _phase; public float _value; }
        /// <summary>종류와 가중치.</summary>
        [Serializable] public sealed class KindWeight { public string _kind; public int _weight; }
        /// <summary>선로 화면 소품 하나: 종류·변형, 거점 기준 오프셋(링 안쪽이 +), 크기.</summary>
        [Serializable] public sealed class MapProp { public string _kind; public int _variant; public float _x; public float _y; public float _scale = 0.5f; }
        /// <summary>선로 화면 거점 둘레 소품 설정.</summary>
        [Serializable]
        public sealed class MapData
        {
            public string[] _propPhases;
            public string[] _campfirePhases;
            public MapProp[] _props;
            public int _groundOrderOffset = -2;
            public int _campfireOrderOffset = -1;
            public float _smokeScale = 0.5f;
            public float _smokeOffsetY = 0.35f;
        }

        public PhaseValue[] _density;
        public KindWeight[] _kinds;
        public string[] _campfirePhases;
        public float _fadeSeconds = 1f;
        public float _framesPerSecond = 2f;
        public float _campfireFramesPerSecond = 6f;
        public float _propScale = 0.9f;
        public float _campfireScale = 1f;
        public float _jitter = 0.1f;
        public int _sortingOffset = 1;
        public float _smokeScale = 0.35f;
        public float _smokeOffsetY = 0.35f;
        public int _seedSalt = 3607;
        public MapData _map;

        /// <summary>phase 단계의 소품 밀도(0..1). 없으면 0.</summary>
        public float Density(IronRing.Contracts.HubPhase phase)
        {
            if (_density == null) return 0f;
            foreach (PhaseValue d in _density) if (d != null && d._phase == phase.ToString()) return Mathf.Clamp01(d._value);
            return 0f;
        }

        /// <summary>phase 단계에 거점 내부 모닥불을 켜는지.</summary>
        public bool CampfireIn(IronRing.Contracts.HubPhase phase) => Has(_campfirePhases, phase);

        /// <summary>list에 phase 이름이 있는지.</summary>
        public static bool Has(string[] list, IronRing.Contracts.HubPhase phase) => list != null && Array.IndexOf(list, phase.ToString()) >= 0;

        /// <summary>roll(음수 아님)로 가중치에 따라 종류 하나를 고른다. 종류가 없으면 null.</summary>
        public string PickKind(int roll)
        {
            if (_kinds == null) return null;
            int total = 0;
            foreach (KindWeight k in _kinds) if (k != null && k._weight > 0) total += k._weight;
            if (total <= 0) return null;
            int r = Mathf.Abs(roll) % total;
            foreach (KindWeight k in _kinds)
            {
                if (k == null || k._weight <= 0) continue;
                if (r < k._weight) return k._kind;
                r -= k._weight;
            }
            return null;
        }

        /// <summary>resource를 읽어 돌려준다. 없으면 오류를 남기고 소품 없는 기본값.</summary>
        public static RuinDressingData Load(string resource)
        {
            TextAsset asset = Resources.Load<TextAsset>(string.IsNullOrEmpty(resource) ? Resource : resource);
            if (asset == null)
            {
                Debug.LogError("RuinDressingData: Resources/" + resource + ".json 이 없습니다.");
                return new RuinDressingData();
            }
            return JsonUtility.FromJson<RuinDressingData>(asset.text) ?? new RuinDressingData();
        }
    }
}
