using UnityEngine;
using UnityEngine.UIElements;

using IronRing.Contracts.Presentation;
using IronRing.Localization;

namespace IronRing.Hud.Interior
{
    /// <summary>
    /// 스토리 012 → 015. 거점 내부 화면의 한국어 HUD: 거점 이름 패널만 보인다. IViewMode.InteriorOpen일 때만 표시한다.
    /// 015에서 잔해 "조사" 메뉴, 남은 조사 횟수, 조사 결과 문구, 자원·아이템 수량 표시를 없앴다(조사는 데이터로 비활성: 스토리 016).
    /// 배치는 핫바(IronRing.Hud.Hotbar.HotbarHud)와 배치 고스트(IronRing.Presentation.Interior.PlacementGhost)가 맡는다.
    /// </summary>
    public sealed class InteriorHud : MonoBehaviour
    {
        private static InteriorHud _instance;
        private bool _bound;
        private IViewMode _viewMode;
        private HudCatalog _catalog;
        private VisualElement _panel;
        private Label _title;

        /// <summary>매 실행 정적 참조를 비우며 이전 런의 HUD 참조를 남기지 않는다(도메인 재로드 비활성 대응).</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { GameServices.Ready -= Create; _instance = null; }

        /// <summary>씬 로드 후 준비 이벤트를 구독하고 등록된 서비스로 HUD 생성을 예약한다.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot() { GameServices.Ready -= Create; GameServices.Ready += Create; }

        /// <summary>스토리 018: 씬(MainUI)에 놓인 문서는 서비스 준비(Initialize) 전까지 꺼 두어 UXML 예시 문구가 Play 화면에 비치지 않게 한다.</summary>
        void Awake() { if (!_bound && TryGetComponent(out UIDocument document)) document.enabled = false; }

        /// <summary>내부 화면 상태가 등록된 씬에서만 HUD 오브젝트를 한 번 만든다.</summary>
        private static void Create()
        {
            if (_instance != null || GameServices.Get<IViewMode>() == null || GameServices.Get<IInteriorAnchors>() == null) return;
            InteriorHud placed = FindAnyObjectByType<InteriorHud>(FindObjectsInactive.Include);
            // 스토리 018: 씬(MainUI)에 놓인 HUD에 바인딩한다. 제작자가 꺼 둔 화면이면 대비책도 만들지 않는다.
            if (placed != null && !(placed.enabled && placed.gameObject.activeInHierarchy)) return;
            _instance = placed != null ? placed : new GameObject(nameof(InteriorHud)).AddComponent<InteriorHud>();
            _instance.Initialize();
        }

        /// <summary>카탈로그와 한국어 문서를 읽고 화면 전환을 구독한다.</summary>
        private void Initialize()
        {
            _viewMode = GameServices.Get<IViewMode>();
            _catalog = JsonUtility.FromJson<HudCatalog>(UnityEngine.Resources.Load<TextAsset>("Hud/HudCatalog").text);
            _bound = true;
            // 스토리 018: 씬에 놓인 UIDocument가 있으면 비어 있는 값만 채운다(Sort Order·PanelSettings·UXML은 프리팹 값 우선).
            UIDocument document = HudDocument.Bind(gameObject, () => KoreanUi.DefaultPanel, 110, "Hud/InteriorHud");
            VisualElement root = document.rootVisualElement;
            root.pickingMode = PickingMode.Ignore;
            foreach (TemplateContainer container in root.Query<TemplateContainer>().ToList()) container.pickingMode = PickingMode.Ignore;
            root.Q("interior").pickingMode = PickingMode.Ignore;
            // UXML과 이름이 다른 USS를 쓴다(같은 Resources 이름은 로드가 모호했다: Story 011 기록).
            StyleSheet style = UnityEngine.Resources.Load<StyleSheet>("Hud/InteriorHudStyle");
            if (style == null) UnityEngine.Debug.LogError("InteriorHud: Resources/Hud/InteriorHudStyle.uss를 불러오지 못했습니다.");
            else if (!root.styleSheets.Contains(style)) root.styleSheets.Add(style);
            KoreanUi.Apply(root);
            _panel = root.Q("interior-panel");
            _panel.pickingMode = PickingMode.Ignore;
            _title = root.Q<Label>("interior-title");
            _viewMode.InteriorChanged += OnInteriorChanged;
            Refresh();
        }

        /// <summary>열린 거점이 있으면 이름 패널을 보이고 아니면 숨긴다.</summary>
        private void Refresh()
        {
            bool open = _viewMode.InteriorOpen;
            _panel.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
            if (open) _title.text = Strings.Get(_catalog.NodeKey(_viewMode.InteriorHubId));
        }

        /// <summary>화면 전환 알림을 받아 패널을 갱신한다.</summary>
        private void OnInteriorChanged(bool open) { Refresh(); }

        void OnDestroy()
        {
            GameServices.Ready -= Create;
            if (_viewMode != null) _viewMode.InteriorChanged -= OnInteriorChanged;
            if (_instance == this) _instance = null;
        }
    }
}
