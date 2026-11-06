using UnityEngine;

using TMPro;

namespace IronRing.Hud.Ui
{
    /// <summary>
    /// 스토리 022. 위 가운데 한 줄 토스트(선로·내부 공통, 항상 켜진 화면 "Toast"). 프리팹의 예시 문구는 편집 모드에서만 보이고 Play 시작 때 숨는다.
    /// HotbarScreen.ShowToast 등은 ToastScreen.Show로 넘긴다.
    /// </summary>
    [RequireComponent(typeof(UiScreen))]
    public sealed class ToastScreen : MonoBehaviour
    {
        private static ToastScreen _instance;

        [SerializeField] private GameObject _box;
        [SerializeField] private TMP_Text _text;
        [Tooltip("스토리 024: 상황 화면이 열려 있는 동안 쌓아 둘 토스트 최대 개수(넘치면 가장 오래된 것을 버린다)")]
        [SerializeField] private int _maxQueued = 4;
        private float _until;
        private bool _overSituation;
        private readonly System.Collections.Generic.Queue<(string text, float seconds)> _queue = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { _instance = null; _swallow = null; _swallowUntil = 0f; }

        private static System.Predicate<string> _swallow;
        private static float _swallowUntil;

        /// <summary>
        /// 스토리 045: 다른 화면(등급 배너)이 같은 소식을 크게 보일 때, match에 맞는 토스트를 seconds초(실시간) 동안 삼킨다.
        /// 이미 떠 있거나 쌓여 있는 같은 토스트도 지운다. 로직 쪽 토스트 호출은 그대로 두고 HUD에서만 겹침을 막는다.
        /// </summary>
        public static void Swallow(System.Predicate<string> match, float seconds)
        {
            if (match == null) return;
            _swallow = match;
            _swallowUntil = Time.realtimeSinceStartup + Mathf.Max(0f, seconds);
            ToastScreen toast = Current;
            if (toast == null) return;
            if (toast._queue.Count > 0)
            {
                var kept = new System.Collections.Generic.List<(string text, float seconds)>(toast._queue);
                toast._queue.Clear();
                foreach (var item in kept) if (!match(item.text)) toast._queue.Enqueue(item);
            }
            if (toast._box != null && toast._box.activeSelf && toast._text != null && match(toast._text.text)) toast._box.SetActive(false);
        }

        private static bool Swallowed(string text) => _swallow != null && Time.realtimeSinceStartup < _swallowUntil && _swallow(text);

        /// <summary>문구를 띄울 수 있는 토스트 화면(켜진 Toast 화면이 없으면 null).</summary>
        private static ToastScreen Current
        {
            get
            {
                if (_instance == null && UiScreens.Instance != null) _instance = UiScreens.Instance.Find<ToastScreen>();
                return _instance != null && _instance.isActiveAndEnabled ? _instance : null;
            }
        }

        /// <summary>UGUI 토스트를 쓸 수 있는지(없으면 호출자가 옛 UI Toolkit 토스트를 쓴다).</summary>
        public static bool Exists => Current != null;

        /// <summary>text를 seconds초(실시간) 띄운다. 토스트 화면이 없거나 꺼져 있으면 false.</summary>
        public static bool Show(string text, float seconds)
        {
            if (IronRing.Save.SaveSlots.IsRestoring) return false; // 스펙 003: 저장 슬롯 복원이 내는 변경 이벤트의 토스트를 띄우지 않도록 추가
            ToastScreen toast = Current;
            if (toast == null || toast._box == null || toast._text == null) return false;
            if (Swallowed(text)) return true;
            if (SituationOpen())
            {
                // 스토리 024: 상황 화면(도감·승객 등)의 제목판과 겹치지 않게, 닫힌 뒤 차례로 띄운다.
                if (toast._queue.Count >= toast._maxQueued) toast._queue.Dequeue();
                toast._queue.Enqueue((text, seconds));
                return true;
            }
            toast.Display(text, seconds);
            return true;
        }

        /// <summary>
        /// 스펙 005. text를 상황 화면이 열려 있어도 기다리지 않고 seconds초 띄운다. 끌어 놓기 실패처럼 바로 이유를 보여야 할 때 쓴다.
        /// 토스트 화면이 없거나 꺼져 있으면 false를 반환한다.
        /// </summary>
        public static bool ShowNow(string text, float seconds)
        {
            ToastScreen toast = Current;
            if (toast == null || toast._box == null || toast._text == null) return false;
            toast.Display(text, seconds);
            toast._overSituation = true;
            return true;
        }

        /// <summary>항상 켜진 화면이 아닌 화면(상황 화면)이 하나라도 열려 있는지.</summary>
        private static bool SituationOpen()
        {
            UiScreens screens = UiScreens.Instance;
            if (screens == null) return false;
            foreach (UiScreen screen in screens.Screens)
                if (screen != null && !screen.AlwaysOn && screens.IsOpen(screen.Id)) return true;
            return false;
        }

        private void Display(string text, float seconds)
        {
            _text.text = text;
            _overSituation = false;
            _until = Time.realtimeSinceStartup + seconds;
            _box.SetActive(true);
        }

        /// <summary>떠 있는 토스트를 바로 숨긴다.</summary>
        public static void Hide()
        {
            ToastScreen toast = Current;
            if (toast != null && toast._box != null) toast._box.SetActive(false);
        }

        void Awake()
        {
            _instance = this;
            if (_box != null) _box.SetActive(false);
        }

        void Update()
        {
            if (_box == null) return;
            if (_box.activeSelf && (Time.realtimeSinceStartup >= _until || (SituationOpen() && !_overSituation))) _box.SetActive(false);
            if (!_box.activeSelf && _queue.Count > 0 && !SituationOpen())
            {
                var next = _queue.Dequeue();
                Display(next.text, next.seconds);
            }
        }

        void OnDestroy() { if (_instance == this) _instance = null; }
    }
}
