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
        private float _until;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { _instance = null; }

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
            ToastScreen toast = Current;
            if (toast == null || toast._box == null || toast._text == null) return false;
            toast._text.text = text;
            toast._until = Time.realtimeSinceStartup + seconds;
            toast._box.SetActive(true);
            return true;
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
            if (_box != null && _box.activeSelf && Time.realtimeSinceStartup >= _until) _box.SetActive(false);
        }

        void OnDestroy() { if (_instance == this) _instance = null; }
    }
}
