using UnityEngine;

using TMPro;

using IronRing.Contracts.Presentation;
using IronRing.Hud.Ui;
using IronRing.Localization;

namespace IronRing.Hud.Interior
{
    /// <summary>
    /// 스토리 022(InteriorHud의 UGUI 포팅). 거점 내부 화면 오른쪽 위 제목 상자(상황 화면 "InteriorTitle"). IViewMode.InteriorOpen일 때만 화면을 켜고
    /// 열린 거점 이름을 쓴다. 참조는 인스펙터로 꽂고, 캔버스·서비스(IViewMode·IInteriorAnchors)가 없으면 조용히 꺼진 채 남는다. 게임 상태는 바꾸지 않는다.
    /// </summary>
    [RequireComponent(typeof(UiScreen))]
    public sealed class InteriorTitleScreen : MonoBehaviour
    {
        private static InteriorTitleScreen _instance;

        [SerializeField] private TMP_Text _title;
        [Tooltip("노드 id → 문자열 키 표(HudCatalog.json).")]
        [SerializeField] private TextAsset _catalogJson;

        private IViewMode _viewMode;
        private HudCatalog _catalog;
        private UiScreens _screens;
        private UiScreen _screen;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { GameServices.Ready -= Create; _instance = null; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot() { GameServices.Ready -= Create; GameServices.Ready += Create; }

        /// <summary>서비스 준비 뒤 MainCanvas에 놓인 화면에 한 번 바인딩한다. 캔버스·화면·서비스가 없거나 스크립트가 꺼져 있으면 아무것도 하지 않는다.</summary>
        private static void Create()
        {
            if (_instance != null || GameServices.Get<IViewMode>() == null || GameServices.Get<IInteriorAnchors>() == null) return;
            UiScreens screens = UiScreens.Instance;
            InteriorTitleScreen placed = screens != null ? screens.Find<InteriorTitleScreen>() : null;
            if (placed == null || !placed.enabled || placed._title == null || placed._catalogJson == null) return;
            _instance = placed;
            placed.Initialize(screens);
        }

        private void Initialize(UiScreens screens)
        {
            _screens = screens;
            _screen = GetComponent<UiScreen>();
            _viewMode = GameServices.Get<IViewMode>();
            _catalog = JsonUtility.FromJson<HudCatalog>(_catalogJson.text);
            _viewMode.InteriorChanged += OnInteriorChanged;
            Refresh();
        }

        /// <summary>열린 거점이 있으면 이름을 쓰고 화면을 켠다. 아니면 끈다.</summary>
        private void Refresh()
        {
            if (_screens == null) return;
            bool open = _viewMode.InteriorOpen;
            if (open) _title.text = Strings.Get(_catalog.NodeKey(_viewMode.InteriorHubId));
            if (open) _screens.Show(_screen.Id); else _screens.Hide(_screen.Id);
        }

        private void OnInteriorChanged(bool open) { Refresh(); }

        void OnDestroy()
        {
            GameServices.Ready -= Create;
            if (_viewMode != null) _viewMode.InteriorChanged -= OnInteriorChanged;
            if (_instance == this) _instance = null;
        }
    }
}
