using System.Collections.Generic;

using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

using TMPro;

using IronRing.Contracts;
using IronRing.Contracts.Presentation;
using IronRing.Hud.Onboarding;
using IronRing.Hud.Ui;
using IronRing.Localization;
using IronRing.Presentation.Interior;
using IronRing.Save;

namespace IronRing.Hud
{
    /// <summary>
    /// 스펙 003 FR-001~FR-005, FR-007~FR-011, FR-013, FR-019 표시. 항상 켜진 화면 "GameMenu".
    /// Esc와 TopBar 톱니 버튼으로 열고 닫으며, 열린 동안 Time.timeScale을 0으로 둔다.
    /// 메뉴·슬롯·확인 패널 가운데 하나만 켜고, 저장·백그라운드 실행·타이틀로·게임 종료를 처리한다.
    /// </summary>
    [RequireComponent(typeof(UiScreen))]
    public sealed class GameMenuScreen : MonoBehaviour
    {
        private const string SCREEN_ID = "GameMenu";
        private const int LOAD_TOAST_DELAY_FRAMES = 2;

        private static GameMenuScreen _instance;
        private static bool _loadFailedToast;

        private enum ConfirmAction { None, Overwrite, ToTitle, Quit }

        [Header("패널")]
        [SerializeField] private GameObject _dim;
        [SerializeField] private GameObject _menuPanel;
        [SerializeField] private GameObject _slotPanel;
        [SerializeField] private GameObject _confirmPanel;

        [Header("메뉴 패널")]
        [SerializeField] private TMP_Text _menuTitle;
        [SerializeField] private Button _saveButton;
        [SerializeField] private TMP_Text _saveBlocked;
        [SerializeField] private Button _backgroundButton;
        [SerializeField] private Button _toTitleButton;
        [SerializeField] private Button _quitButton;

        [Header("슬롯 패널")]
        [SerializeField] private TMP_Text _slotTitle;
        [SerializeField] private Button[] _slotButtons;
        [SerializeField] private Button _slotClose;

        [Header("확인 패널")]
        [SerializeField] private TMP_Text _confirmMessage;
        [SerializeField] private Button _yesButton;
        [SerializeField] private Button _noButton;

        [Header("토스트")]
        [Tooltip("저장 결과·불러오기 실패 토스트 표시 시간(초, 실시간).")]
        [SerializeField] private float _toastSeconds = 2.5f;

        [Header("상태")]
        private float _previousTimeScale = 1f;
        private bool _isOpen;
        private ConfirmAction _confirm;
        private int _pendingSlot;
        private int _escConsumedFrame = -1;
        private int _readyFrame = -1;
        private bool _loadToastDone;
        private UiScreens _screens;
        private PlacementGhost _ghost;

        /// <summary>게임 메뉴가 열려 있으면 true.</summary>
        public static bool IsOpen => _instance != null && _instance._isOpen;

        /// <summary>도메인 재로드가 꺼져 있으므로 플레이 진입마다 정적 상태를 지운다.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { _instance = null; _loadFailedToast = false; }

        void Awake()
        {
            _instance = this;
            _menuTitle.text = Strings.Get("game_menu_title");
            SetLabel(_saveButton, Strings.Get("game_menu_save"));
            _saveBlocked.text = Strings.Get("game_menu_save_blocked");
            SetLabel(_toTitleButton, Strings.Get("game_menu_to_title"));
            SetLabel(_quitButton, Strings.Get("game_menu_quit"));
            _slotTitle.text = Strings.Get("game_menu_save");
            SetLabel(_slotClose, Strings.Get("game_menu_close"));
            SetLabel(_yesButton, Strings.Get("game_menu_yes"));
            SetLabel(_noButton, Strings.Get("game_menu_no"));
            _saveButton.onClick.AddListener(ShowSlots);
            _backgroundButton.onClick.AddListener(ToggleBackground);
            _toTitleButton.onClick.AddListener(RequestToTitle);
            _quitButton.onClick.AddListener(RequestQuit);
            _slotClose.onClick.AddListener(ShowMenu);
            for (int i = 0; i < _slotButtons.Length; i++)
            {
                int slot = i + 1;
                _slotButtons[i].onClick.AddListener(() => PickSlot(slot));
            }
            _yesButton.onClick.AddListener(ConfirmYes);
            _noButton.onClick.AddListener(ConfirmNo);
            ShowPanel(null);
        }

        void Start()
        {
            _screens = UiScreens.Instance;
            if (_screens != null) _screens.ScreenChanged += OnScreenChanged;
        }

        void Update()
        {
            TickLoadFailedToast();
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.escapeKey.wasPressedThisFrame) return;
            if (DevSituationsScreen.IsOpen) return;
            // NPC 끌기 취소에 쓴 Esc는 메뉴를 열지 않는다.
            if (IronRing.Presentation.HubPhase.NpcDragController.IsInputHeld) return;
            if (_isOpen) { Back(); return; }
            if (EscUsedElsewhere()) return;
            Open();
        }

        void OnDestroy()
        {
            if (_screens != null) _screens.ScreenChanged -= OnScreenChanged;
            if (_ghost != null) _ghost.SelectionChanged -= OnGhostSelectionChanged;
            // 메뉴가 열린 채 씬이 바뀌면 멈춘 시간을 되돌린다.
            if (_isOpen) Time.timeScale = _previousTimeScale;
            if (_instance == this) _instance = null;
        }

        /// <summary>
        /// TopBar 톱니 버튼이 부른다. 닫혀 있으면 메뉴를 열고(FR-001), 열려 있으면 한 단계 닫는다(FR-002).
        /// 씬에 게임 메뉴가 없으면 아무것도 하지 않는다.
        /// </summary>
        public static void Toggle()
        {
            if (_instance == null) return;
            if (_instance._isOpen) _instance.Back();
            else _instance.Open();
        }

        /// <summary>시간 배율을 _previousTimeScale에 기억하고 0으로 둔 뒤 메뉴 패널을 연다(FR-001). 이미 열려 있으면 그대로 둔다.</summary>
        private void Open()
        {
            if (_isOpen) return;
            _previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            _isOpen = true;
            ShowMenu();
        }

        /// <summary>모든 패널을 닫고 Time.timeScale을 _previousTimeScale로 되돌린다(FR-002).</summary>
        private void Close()
        {
            if (!_isOpen) return;
            _isOpen = false;
            _confirm = ConfirmAction.None;
            ShowPanel(null);
            Time.timeScale = _previousTimeScale;
        }

        /// <summary>열린 패널을 한 단계 닫는다. 확인·슬롯 패널은 메뉴 패널로, 메뉴 패널은 메뉴 전체 닫기로 간다(FR-002).</summary>
        private void Back()
        {
            if (_menuPanel.activeSelf) Close();
            else ShowMenu();
        }

        /// <summary>
        /// 메뉴 패널을 켜고 백그라운드 실행 문구와 저장 가능 여부를 다시 그린다(FR-003, FR-005, FR-009).
        /// IPhaseLoop.Phase가 순환이면 저장 버튼을 끄고 이유 문구를 보인다.
        /// </summary>
        private void ShowMenu()
        {
            _confirm = ConfirmAction.None;
            ShowPanel(_menuPanel);
            RefreshBackgroundLabel();
            IPhaseLoop loop = GameServices.Get<IPhaseLoop>();
            bool blocked = loop != null && loop.Phase == LoopPhase.Circulate;
            _saveButton.interactable = !blocked;
            _saveBlocked.gameObject.SetActive(blocked);
        }

        /// <summary>슬롯 패널을 켜고 SaveSlots.List() 결과로 칸 문구를 채운다(FR-004).</summary>
        private void ShowSlots()
        {
            ShowPanel(_slotPanel);
            RefreshSlots();
        }

        /// <summary>SaveSlots.List()를 읽어 칸 버튼마다 번호 줄과 빈 칸·손상·요약 줄을 쓴다(FR-004).</summary>
        private void RefreshSlots()
        {
            SaveSlotInfo[] slots = SaveSlots.List();
            for (int i = 0; i < _slotButtons.Length; i++)
            {
                string detail = i >= slots.Length || slots[i].IsEmpty ? Strings.Get("save_slot_empty")
                    : slots[i].IsCorrupt ? Strings.Get("save_slot_corrupt")
                    : slots[i].Summary;
                SetLabel(_slotButtons[i], string.Format(Strings.Get("save_slot_label"), i + 1) + "\n" + detail);
            }
        }

        /// <summary>slot 칸을 골랐다. 저장(손상 포함)이 있으면 _pendingSlot에 적고 덮어쓰기 확인을 띄우며, 비었으면 바로 저장한다(FR-007).</summary>
        private void PickSlot(int slot)
        {
            SaveSlotInfo[] slots = SaveSlots.List();
            bool occupied = slot <= slots.Length && !slots[slot - 1].IsEmpty;
            if (!occupied) { SaveTo(slot); return; }
            _pendingSlot = slot;
            ShowConfirm(ConfirmAction.Overwrite, "game_menu_overwrite_confirm");
        }

        /// <summary>SaveSlots.Save(slot)을 부르고 슬롯 패널 요약을 다시 그린 뒤 성공·실패 토스트를 띄운다(FR-008).</summary>
        private void SaveTo(int slot)
        {
            bool saved = SaveSlots.Save(slot);
            ShowSlots();
            ToastScreen.Show(Strings.Get(saved ? "game_menu_saved" : "game_menu_save_failed"), _toastSeconds);
        }

        /// <summary>BackgroundRunSetting을 뒤집고 버튼 문구를 바꾼다(FR-009).</summary>
        private void ToggleBackground()
        {
            BackgroundRunSetting.Toggle();
            RefreshBackgroundLabel();
        }

        /// <summary>BackgroundRunSetting.IsOn에 맞춰 백그라운드 실행 버튼에 켜짐·꺼짐 문구를 쓴다.</summary>
        private void RefreshBackgroundLabel()
        {
            SetLabel(_backgroundButton, Strings.Get(BackgroundRunSetting.IsOn ? "game_menu_background_on" : "game_menu_background_off"));
        }

        /// <summary>SaveSlots.HasUnsavedProgress()가 참이면 나가기 확인을 띄우고, 거짓이면 바로 타이틀로 간다(FR-010).</summary>
        private void RequestToTitle()
        {
            if (SaveSlots.HasUnsavedProgress()) ShowConfirm(ConfirmAction.ToTitle, "game_menu_leave_confirm");
            else GoToTitle();
        }

        /// <summary>SaveSlots.HasUnsavedProgress()가 참이면 종료 확인을 띄우고, 거짓이면 바로 끈다(FR-011).</summary>
        private void RequestQuit()
        {
            if (SaveSlots.HasUnsavedProgress()) ShowConfirm(ConfirmAction.Quit, "game_menu_quit_confirm");
            else Quit();
        }

        /// <summary>action을 _confirm에 기억하고 messageKey 문구로 확인 패널을 켠다.</summary>
        private void ShowConfirm(ConfirmAction action, string messageKey)
        {
            _confirm = action;
            _confirmMessage.text = Strings.Get(messageKey);
            ShowPanel(_confirmPanel);
        }

        /// <summary>확인 "예": _confirm에 기억한 동작(덮어쓰기 저장·타이틀로·종료)을 실행한다.</summary>
        private void ConfirmYes()
        {
            ConfirmAction action = _confirm;
            _confirm = ConfirmAction.None;
            switch (action)
            {
                case ConfirmAction.Overwrite: SaveTo(_pendingSlot); break;
                case ConfirmAction.ToTitle: GoToTitle(); break;
                case ConfirmAction.Quit: Quit(); break;
                default: ShowMenu(); break;
            }
        }

        /// <summary>확인 "아니오": 덮어쓰기는 슬롯 패널로, 나머지는 메뉴 패널로 돌아간다.</summary>
        private void ConfirmNo()
        {
            if (_confirm == ConfirmAction.Overwrite) { _confirm = ConfirmAction.None; ShowSlots(); }
            else ShowMenu();
        }

        /// <summary>_isOpen을 내리고 Time.timeScale을 1로 되돌린 뒤 타이틀 씬을 연다(FR-010).</summary>
        private void GoToTitle()
        {
            _isOpen = false;
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneNames.Title);
        }

        /// <summary>SaveSlots 종료 허용 표시를 세우고 게임을 끈다(FR-011). 편집기에서는 Play Mode를 멈춘다.</summary>
        private void Quit()
        {
            SaveSlots.AllowQuit();
#if UNITY_EDITOR
            // 편집기에서는 Application.Quit이 무시되므로 Play Mode를 멈춰 종료를 보이게 한다.
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        /// <summary>panel 하나만 켜고 나머지 패널은 끈다. null이면 Dim까지 모두 끈다.</summary>
        private void ShowPanel(GameObject panel)
        {
            _dim.SetActive(panel != null);
            _menuPanel.SetActive(panel == _menuPanel);
            _slotPanel.SetActive(panel == _slotPanel);
            _confirmPanel.SetActive(panel == _confirmPanel);
        }

        /// <summary>
        /// 같은 프레임의 Esc를 다른 화면이 썼는지 돌려준다. 상황 화면(도감·승객·대화 등)이 열려 있거나 이번 프레임에 닫혔거나,
        /// 배치 중인 재료 선택·끌기가 있거나 이번 프레임에 취소됐으면 true다.
        /// </summary>
        private bool EscUsedElsewhere()
        {
            if (_escConsumedFrame == Time.frameCount) return true;
            if (_screens != null)
            {
                IReadOnlyList<UiScreen> screens = _screens.Screens;
                for (int i = 0; i < screens.Count; i++)
                    if (screens[i] != null && !screens[i].AlwaysOn && screens[i].gameObject.activeSelf) return true;
            }
            ResolveGhost();
            return _ghost != null && (_ghost.Selected.IsValid || _ghost.IsDragging);
        }

        /// <summary>PlacementGhost.Instance를 늦게라도 _ghost에 받아 선택 변경을 구독한다.</summary>
        private void ResolveGhost()
        {
            if (_ghost != null || PlacementGhost.Instance == null) return;
            _ghost = PlacementGhost.Instance;
            _ghost.SelectionChanged += OnGhostSelectionChanged;
        }

        /// <summary>id 상황 화면이 닫힌 프레임을 _escConsumedFrame에 적는다. 그 화면이 Esc로 닫혔을 때 메뉴가 같이 열리지 않게 한다.</summary>
        private void OnScreenChanged(string id, bool open)
        {
            if (!open && id != SCREEN_ID) _escConsumedFrame = Time.frameCount;
        }

        /// <summary>배치 선택이 바뀐 프레임을 _escConsumedFrame에 적는다. Esc로 선택을 취소했을 때 메뉴가 같이 열리지 않게 한다.</summary>
        private void OnGhostSelectionChanged(PlacementEntry entry)
        {
            _escConsumedFrame = Time.frameCount;
        }

        /// <summary>
        /// SaveSlots의 불러오기 실패 표시를 _loadFailedToast로 받아 두었다가, 서비스 준비 뒤 LOAD_TOAST_DELAY_FRAMES 프레임이 지나면
        /// save_load_failed 토스트를 띄운다(FR-019). 실패한 씬은 곧 다시 열리므로 표시는 정적 필드로 넘겨 다시 연 씬의 화면이 띄운다.
        /// </summary>
        private void TickLoadFailedToast()
        {
            if (_loadToastDone) return;
            if (SaveSlots.ConsumeLoadFailed()) _loadFailedToast = true;
            if (!GameServices.IsReady) return;
            if (_readyFrame < 0) { _readyFrame = Time.frameCount; return; }
            if (Time.frameCount < _readyFrame + LOAD_TOAST_DELAY_FRAMES) return;
            _loadToastDone = true;
            if (!_loadFailedToast) return;
            _loadFailedToast = false;
            ToastScreen.Show(Strings.Get("save_load_failed"), _toastSeconds);
        }

        /// <summary>button 안 첫 TMP 라벨에 text를 쓴다.</summary>
        private static void SetLabel(Button button, string text)
        {
            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
            if (label != null) label.text = text;
        }
    }
}
