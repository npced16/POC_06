using System;
using System.Collections.Generic;

using UnityEngine;

namespace IronRing.Hud.Ui
{
    /// <summary>
    /// 스토리 022. MainCanvas 루트의 화면 목록과 전환. 화면 루트를 SetActive로 켜고 끈다(그리는 순서 = 하이어라키 순서).
    /// Play 시작(Awake) 때 AlwaysOn이 아닌 화면을 끈다. 게임 상태는 갖지 않는다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UiScreens : MonoBehaviour
    {
        private static UiScreens _instance;

        [SerializeField] private UiTheme _theme;
        [SerializeField] private List<UiScreen> _screens = new List<UiScreen>();

        /// <summary>화면이 켜지거나 꺼질 때(id, 켜짐).</summary>
        public event Action<string, bool> ScreenChanged;

        /// <summary>씬의 UiScreens(없으면 null). 처음 부를 때 씬에서 찾는다.</summary>
        public static UiScreens Instance
        {
            get
            {
                if (_instance == null) _instance = FindAnyObjectByType<UiScreens>();
                return _instance;
            }
        }

        /// <summary>캔버스가 쓰는 테마.</summary>
        public UiTheme Theme => _theme;

        /// <summary>등록된 화면(그리는 순서).</summary>
        public IReadOnlyList<UiScreen> Screens => _screens;

        /// <summary>매 실행 정적 참조를 비운다(도메인 재로드 비활성 대응).</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { _instance = null; }

        void Awake()
        {
            _instance = this;
            foreach (UiScreen screen in _screens)
                if (screen != null && !screen.AlwaysOn) screen.gameObject.SetActive(false);
        }

        void OnDestroy() { if (_instance == this) _instance = null; }

        /// <summary>id 화면(없으면 null).</summary>
        public UiScreen Get(string id)
        {
            foreach (UiScreen screen in _screens) if (screen != null && screen.Id == id) return screen;
            return null;
        }

        /// <summary>등록된 화면 루트에서 T 컴포넌트를 찾는다(꺼진 화면 포함). 화면 스크립트가 서비스 준비 뒤 자기 인스턴스를 얻을 때 쓴다.</summary>
        public T Find<T>() where T : Component
        {
            foreach (UiScreen screen in _screens)
                if (screen != null && screen.TryGetComponent(out T found)) return found;
            return null;
        }

        /// <summary>id 화면을 켠다. 없으면 false.</summary>
        public bool Show(string id) => Set(id, true);

        /// <summary>id 화면을 끈다. 없으면 false.</summary>
        public bool Hide(string id) => Set(id, false);

        /// <summary>id 화면을 뒤집는다. 없으면 false.</summary>
        public bool Toggle(string id) => Set(id, !IsOpen(id));

        /// <summary>id 화면이 켜져 있는지.</summary>
        public bool IsOpen(string id)
        {
            UiScreen screen = Get(id);
            return screen != null && screen.gameObject.activeSelf;
        }

        private bool Set(string id, bool open)
        {
            UiScreen screen = Get(id);
            if (screen == null) return false;
            if (screen.gameObject.activeSelf == open) return true;
            screen.gameObject.SetActive(open);
            ScreenChanged?.Invoke(id, open);
            return true;
        }
    }
}
