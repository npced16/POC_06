using System;
using System.Collections.Generic;

using UnityEngine;

using TMPro;

namespace IronRing.Presentation.Travel
{
    /// <summary>
    /// 스토리 037. 여정 화면을 MainSettings._travelView("side" | "map")로 고른다. TravelSideView.Awake가 정하고,
    /// RaidView(지도 위 습격 표현)는 이 값이 true면 쉰다. 씬에 TravelSide가 없으면 false(예전 동작).
    /// </summary>
    public static class TravelViewMode
    {
        /// <summary>횡스크롤 여정 화면이 켜져 있으면 true.</summary>
        public static bool IsSide { get; set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { IsSide = false; }

        /// <summary>MainSettings.json 텍스트에서 _travelView를 읽는다(없으면 "side").</summary>
        public static string Parse(string json)
        {
            if (string.IsNullOrEmpty(json)) return "side";
            var settings = JsonUtility.FromJson<Settings>(json);
            return string.IsNullOrEmpty(settings?._travelView) ? "side" : settings._travelView;
        }

        [Serializable]
        private sealed class Settings { public string _travelView; }
    }

    /// <summary>스토리 037. 구간 데이터(Resources/Track/travel-side.json): 여정 시간·토스트 설정·지형지물 목록.</summary>
    [Serializable]
    public sealed class TravelSideData
    {
        /// <summary>횡스크롤 모드에서 한 구간 걸리는 시간(초). TrainController.EdgeSecondsOverride로 넣는다(0 = 선로 데이터 그대로).</summary>
        public float _edgeSeconds = 24f;
        /// <summary>라벨이 있는 지형지물을 지날 때 "…을 지나는 중" 토스트를 띄울지.</summary>
        public bool _passToasts = true;
        /// <summary>토스트 표시 시간(초).</summary>
        public float _toastSeconds = 1.6f;
        /// <summary>지형지물 목록.</summary>
        public TravelLandmark[] _landmarks = Array.Empty<TravelLandmark>();

        /// <summary>Resources에서 읽는다(없으면 빈 데이터).</summary>
        public static TravelSideData Load()
        {
            TextAsset json = Resources.Load<TextAsset>("Track/travel-side");
            return json != null ? JsonUtility.FromJson<TravelSideData>(json.text) : new TravelSideData();
        }
    }

    /// <summary>스토리 037. 지형지물 하나: 구간 진행도 위치(0 = 출발 역, 1 = 종점), 세계 단위 보정, 종류, 라벨, 토스트 여부.</summary>
    [Serializable]
    public sealed class TravelLandmark
    {
        /// <summary>구간 진행도 위치 0..1.</summary>
        public float _progress;
        /// <summary>진행도 위치에서 더하는 세계 단위 x 보정(예: 역 승강장을 열차 아래로).</summary>
        public float _offset;
        /// <summary>station | signal | halt | bridge | watertower | outpost.</summary>
        public string _kind;
        /// <summary>보이는 라벨(한국어, 비면 라벨 없음).</summary>
        public string _label;
        /// <summary>지날 때 토스트를 띄울지.</summary>
        public bool _toast;
    }

    /// <summary>
    /// 스토리 037. 색 사각형·원·라벨 프리미티브를 카탈로그 프리팹(PrimRect/PrimCircle/PrimLabel)으로 만든다.
    /// 프리팹이 비어 있으면 같은 구성의 오브젝트를 코드로 만든다(생성기 전에도 동작).
    /// </summary>
    public sealed class TravelPrimitives
    {
        private readonly TravelCatalog _catalog;
        private static Sprite _fallbackWhite;

        /// <summary>catalog의 프리팹·스프라이트를 쓰는 헬퍼.</summary>
        public TravelPrimitives(TravelCatalog catalog) { _catalog = catalog; }

        /// <summary>parent 아래 로컬 (x, y)에 가로 w·세로 h 색 사각형(가운데 기준)을 만든다.</summary>
        public SpriteRenderer Rect(Transform parent, string name, float x, float y, float w, float h, Color color, int order)
            => Shape(_catalog != null ? _catalog.RectPrefab : null, _catalog != null ? _catalog.WhiteSprite : null, parent, name, x, y, w, h, color, order);

        /// <summary>parent 아래 로컬 (x, y)에 지름 d 색 원을 만든다.</summary>
        public SpriteRenderer Circle(Transform parent, string name, float x, float y, float d, Color color, int order)
            => Shape(_catalog != null ? _catalog.CirclePrefab : null, _catalog != null ? _catalog.CircleSprite : null, parent, name, x, y, d, d, color, order);

        /// <summary>parent 아래 로컬 (x, y)에 월드 TMP 라벨을 만든다.</summary>
        public TMP_Text Label(Transform parent, string name, float x, float y, string text, float size, Color color, int order)
        {
            GameObject go = _catalog != null && _catalog.LabelPrefab != null ? UnityEngine.Object.Instantiate(_catalog.LabelPrefab, parent, false) : new GameObject();
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(x, y, 0f);
            TMP_Text label = go.GetComponent<TMP_Text>();
            if (label == null) { var tmp = go.AddComponent<TextMeshPro>(); tmp.alignment = TextAlignmentOptions.Center; label = tmp; }
            label.text = text;
            label.fontSize = size;
            label.color = color;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            if (label is TextMeshPro world) world.sortingOrder = order;
            return label;
        }

        /// <summary>parent의 자식을 모두 지운다.</summary>
        public static void Clear(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--) UnityEngine.Object.Destroy(parent.GetChild(i).gameObject);
        }

        private static SpriteRenderer Shape(GameObject prefab, Sprite sprite, Transform parent, string name, float x, float y, float w, float h, Color color, int order)
        {
            GameObject go = prefab != null ? UnityEngine.Object.Instantiate(prefab, parent, false) : new GameObject();
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(x, y, 0f);
            go.transform.localScale = new Vector3(w, h, 1f);
            SpriteRenderer renderer = go.GetComponent<SpriteRenderer>() ?? go.AddComponent<SpriteRenderer>();
            if (renderer.sprite == null) renderer.sprite = sprite != null ? sprite : FallbackWhite();
            renderer.color = color;
            renderer.sortingOrder = order;
            return renderer;
        }

        private static Sprite FallbackWhite()
        {
            if (_fallbackWhite == null) _fallbackWhite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
            return _fallbackWhite;
        }
    }

    /// <summary>스토리 037. 클릭 사격 대상 표시(몬스터 id 또는 둥지 순번). BoxCollider2D와 함께 붙는다.</summary>
    public sealed class TravelRaidTarget : MonoBehaviour
    {
        /// <summary>둥지면 true.</summary>
        public bool IsNest;
        /// <summary>몬스터 id 또는 둥지 순번.</summary>
        public int Id;
    }

    /// <summary>스토리 037. 잠깐 보였다 사라지는 프리미티브(연기·조각·번쩍임·포탄). 실시간이 아니라 게임 시간으로 움직인다.</summary>
    public sealed class TravelTransient : MonoBehaviour
    {
        private Vector3 _velocity;
        private Vector3 _from, _to;
        private bool _travel;
        private float _life, _age, _grow, _spin;
        private SpriteRenderer _renderer;
        private Color _color;

        /// <summary>속도(로컬/초)·수명·크기 증가율·회전(도/초)으로 떠다니다 흐려진다.</summary>
        public void Drift(Vector3 velocity, float life, float grow, float spin)
        {
            _velocity = velocity; _life = Mathf.Max(0.01f, life); _grow = grow; _spin = spin; _travel = false; Init();
        }

        /// <summary>from에서 to(월드)로 life초 동안 날아간다.</summary>
        public void Fly(Vector3 from, Vector3 to, float life)
        {
            _from = from; _to = to; _life = Mathf.Max(0.01f, life); _travel = true; transform.position = from; Init();
        }

        private void Init()
        {
            _renderer = GetComponent<SpriteRenderer>();
            if (_renderer != null) _color = _renderer.color;
        }

        private void Update()
        {
            _age += Time.deltaTime;
            float t = Mathf.Clamp01(_age / _life);
            if (_travel) transform.position = Vector3.Lerp(_from, _to, t);
            else
            {
                transform.localPosition += _velocity * Time.deltaTime;
                transform.localScale += Vector3.one * _grow * Time.deltaTime;
                transform.Rotate(0f, 0f, _spin * Time.deltaTime);
                if (_renderer != null) _renderer.color = new Color(_color.r, _color.g, _color.b, _color.a * (1f - t));
            }
            if (_age >= _life) Destroy(gameObject);
        }
    }

    /// <summary>스토리 037. 한국어 조사(을/를) 고르기.</summary>
    public static class KoreanParticle
    {
        /// <summary>word 끝 글자에 받침이 있으면 "을", 없으면 "를".</summary>
        public static string ObjectParticle(string word)
        {
            if (string.IsNullOrEmpty(word)) return "를";
            char last = word[word.Length - 1];
            if (last < 0xAC00 || last > 0xD7A3) return "을";
            return (last - 0xAC00) % 28 == 0 ? "를" : "을";
        }
    }
}
