using UnityEngine;
using UnityEngine.UI;

using TMPro;

using IronRing.Hud.Ui;

namespace IronRing.Hud
{
    /// <summary>
    /// 스토리 022(StateCombatEffects의 UI Toolkit 오버레이 포팅). 항상 켜진 화면 "CombatFx":
    /// 월드 위치에서 떠오르는 문구(FloatingMessage 부품 풀)와 전체 화면 그늘(패배 제목·본문). 스프라이트·월드 연출은 StateCombatEffects에 남는다.
    /// StateCombatEffects는 정적 API(Message·ShowShade)로만 부른다. 화면이 없으면 호출은 false를 돌려주고 아무것도 하지 않는다.
    /// </summary>
    [RequireComponent(typeof(UiScreen))]
    public sealed class CombatFxScreen : MonoBehaviour
    {
        private static CombatFxScreen _instance;

        [Tooltip("문구가 놓이는 화면 전체 영역.")]
        [SerializeField] private RectTransform _area;
        [Tooltip("미리 놓아 둔 문구 부품(FloatingMessage.prefab). 모자라면 가장 오래된 것을 다시 쓴다.")]
        [SerializeField] private FloatingMessageView[] _pool = new FloatingMessageView[0];
        [SerializeField] private Image _shade;
        [SerializeField] private TMP_Text _shadeTitle;
        [SerializeField] private TMP_Text _shadeBody;

        private Entry[] _entries = new Entry[0];

        private struct Entry
        {
            public bool Active;
            public Vector3 World;
            public float Remaining;
            public float RiseSpeed;
            public string Group;
            public Camera Camera;
            public float Started;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { _instance = null; }

        private static CombatFxScreen Current
        {
            get
            {
                if (_instance == null && UiScreens.Instance != null) _instance = UiScreens.Instance.Find<CombatFxScreen>();
                return _instance != null && _instance.isActiveAndEnabled ? _instance : null;
            }
        }

        /// <summary>UGUI 전투 문구 화면을 쓸 수 있는지.</summary>
        public static bool Available => Current != null;

        /// <summary>
        /// world 위치에 text를 띄우고 seconds초(실시간) 동안 riseSpeed(월드 단위/초)로 올린다. group이 있으면 같은 group의 이전 문구를 바꾼다.
        /// camera로 화면 좌표를 계산한다. 화면이 없으면 false.
        /// </summary>
        public static bool Message(Vector3 world, string text, string group, float seconds, float riseSpeed, Camera camera)
        {
            CombatFxScreen screen = Current;
            if (screen == null || screen._pool.Length == 0 || camera == null) return false;
            screen.Add(world, text, group, seconds, riseSpeed, camera);
            return true;
        }

        /// <summary>전체 화면 그늘(color)에 패배 제목·본문을 띄운다. 화면이 없으면 false.</summary>
        public static bool ShowShade(string title, string body, Color color)
        {
            CombatFxScreen screen = Current;
            if (screen == null || screen._shade == null) return false;
            screen._shade.color = color;
            if (screen._shadeTitle != null) screen._shadeTitle.text = title;
            if (screen._shadeBody != null) screen._shadeBody.text = body;
            screen._shade.gameObject.SetActive(true);
            return true;
        }

        void Awake()
        {
            _instance = this;
            _entries = new Entry[_pool.Length];
            foreach (FloatingMessageView view in _pool) if (view != null) view.gameObject.SetActive(false);
            if (_shade != null) _shade.gameObject.SetActive(false);
        }

        private void Add(Vector3 world, string text, string group, float seconds, float riseSpeed, Camera camera)
        {
            int slot = -1;
            if (group != null)
                for (int i = 0; i < _entries.Length; i++)
                    if (_entries[i].Active && _entries[i].Group == group) { slot = i; break; }
            if (slot < 0)
                for (int i = 0; i < _entries.Length; i++)
                    if (!_entries[i].Active) { slot = i; break; }
            if (slot < 0)
            {
                slot = 0;
                for (int i = 1; i < _entries.Length; i++) if (_entries[i].Started < _entries[slot].Started) slot = i;
            }
            FloatingMessageView view = _pool[slot];
            if (view == null) return;
            _entries[slot] = new Entry { Active = true, World = world, Remaining = seconds, RiseSpeed = riseSpeed, Group = group, Camera = camera, Started = Time.realtimeSinceStartup };
            if (view.Label != null) view.Label.text = text;
            view.gameObject.SetActive(true);
            view.transform.SetAsLastSibling();
            Place(slot);
        }

        /// <summary>slot 문구를 월드 위치에 대응하는 화면 위치로 옮긴다(Overlay 캔버스라 카메라 인자는 null).</summary>
        private void Place(int slot)
        {
            Entry entry = _entries[slot];
            RectTransform area = _area != null ? _area : (RectTransform)transform;
            Vector3 screen = entry.Camera.WorldToScreenPoint(entry.World);
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(area, screen, null, out Vector2 local))
                _pool[slot].Rect.anchoredPosition = local;
        }

        void LateUpdate()
        {
            float delta = Time.unscaledDeltaTime;
            for (int i = 0; i < _entries.Length; i++)
            {
                if (!_entries[i].Active) continue;
                _entries[i].Remaining -= delta;
                if (_entries[i].Remaining <= 0 || _entries[i].Camera == null)
                {
                    _entries[i].Active = false;
                    if (_pool[i] != null) _pool[i].gameObject.SetActive(false);
                    continue;
                }
                _entries[i].World += Vector3.up * (_entries[i].RiseSpeed * delta);
                if (_pool[i] != null) Place(i);
            }
        }

        void OnDestroy() { if (_instance == this) _instance = null; }
    }
}
