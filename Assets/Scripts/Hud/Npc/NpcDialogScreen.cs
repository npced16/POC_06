using System.Collections.Generic;
using System.Text;

using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

using TMPro;

using IronRing.Contracts;
using IronRing.Contracts.Presentation;
using IronRing.Hud.Ui;
using IronRing.Localization;
using IronRing.Npc.Demo;
using IronRing.Presentation.HubPhase;

namespace IronRing.Hud.Npc
{
    /// <summary>
    /// 스토리 022(NpcDialogHud의 UGUI 포팅). 상황 화면 "NpcDialog": INpcClickSource.NpcClicked로 열고 직업명·집 서식지·출신지(스토리 025)·대사·결과 한 줄·
    /// 선택지 버튼(INpcInteraction.Options, 부품 DialogOption)·[닫기]를 보인다. 닫기 버튼·Esc·내부 화면 닫힘·다른 거점이면 닫고,
    /// 다른 NPC 클릭이면 그 NPC로 바꾼다. 게임 상태는 바꾸지 않고 TryInteract 명령만 보낸다. 데모 단계(NpcHomesDemoDriver.StageReady)는 HandleDemoStage가 처리한다.
    /// </summary>
    [RequireComponent(typeof(UiScreen))]
    public sealed class NpcDialogScreen : MonoBehaviour
    {
        private const string DEMO_HUB = "hub-1";
        private const string ENCOURAGE_OPTION = "encourage";
        private static NpcDialogScreen _instance;
        private static bool _loggedMissing;

        /// <summary>현재 대화 화면(데모용). 초기화 전이면 null.</summary>
        public static NpcDialogScreen Instance => _instance;

        [SerializeField] private TMP_Text _title;
        [SerializeField] private TMP_Text _home;
        [Tooltip("스토리 025: 집 줄 옆 \"<출신지> 출신\".")]
        [SerializeField] private TMP_Text _origin;
        [SerializeField] private TMP_Text _line;
        [SerializeField] private TMP_Text _result;
        [SerializeField] private TMP_Text _reasons;
        [SerializeField] private RectTransform _options;
        [Tooltip("선택지 버튼 부품(Parts/DialogOption.prefab).")]
        [SerializeField] private DialogOptionView _optionPrefab;
        [SerializeField] private Button _close;
        [Tooltip("스토리 027: 대화 상자(대사 위 클릭 = 타자기 즉시 완성 영역).")]
        [SerializeField] private RectTransform _panel;
        [Tooltip("스토리 027: NPC 초상(몸체 스프라이트를 키운 상자).")]
        [SerializeField] private Image _portrait;
        [Tooltip("스토리 027: 타자기 속도(초당 글자). 0이면 한 번에 보인다.")]
        [SerializeField] private float _typeSpeed = 28f;
        [Tooltip("스토리 027: 닫기 버튼의 대화 상자 오른쪽 위 모서리 기준 위치(pad 안쪽).")]
        [SerializeField] private Vector2 _closeInset = new Vector2(-12f, -12f);

        private UiScreens _screens;
        private UiScreen _screen;
        private UiTheme _theme;
        private IViewMode _view;
        private INpcInteraction _interaction;
        private INpcHomes _homes;
        private INpcClickSource _clicks;
        private string _hub;
        private string _npc;
        private string _typedLine;
        private float _typed;
        private bool _typing;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { GameServices.Ready -= Create; _instance = null; _loggedMissing = false; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot() { GameServices.Ready -= Create; GameServices.Ready += Create; }

        /// <summary>내부 화면(IViewMode)이 있고 캔버스에 화면이 있으면 한 번 바인딩한다.</summary>
        private static void Create()
        {
            if (_instance != null || GameServices.Get<IViewMode>() == null) return;
            UiScreens screens = UiScreens.Instance;
            NpcDialogScreen placed = screens != null ? screens.Find<NpcDialogScreen>() : null;
            if (placed == null || !placed.enabled || placed._optionPrefab == null || placed._options == null) return;
            _instance = placed;
            placed.Initialize(screens);
        }

        private void Initialize(UiScreens screens)
        {
            _screens = screens;
            _screen = GetComponent<UiScreen>();
            _theme = screens.Theme;
            _view = GameServices.Get<IViewMode>();
            TMP_Text closeLabel = _close.GetComponentInChildren<TMP_Text>(true);
            if (closeLabel != null) closeLabel.text = Strings.Get("npc_dialog_close");
            _close.onClick.AddListener(Close);
            // 스토리 027: 닫기 버튼은 제목 줄과 분리해 대화 상자 오른쪽 위 모서리(pad 안쪽)에 런타임에서도 고정한다(씬·레이아웃 재정의 방지).
            if (_panel != null)
            {
                var closeRect = (RectTransform)_close.transform;
                if (closeRect.parent != _panel) closeRect.SetParent(_panel, false);
                LayoutElement ignore = _close.GetComponent<LayoutElement>() ?? _close.gameObject.AddComponent<LayoutElement>();
                ignore.ignoreLayout = true;
                closeRect.anchorMin = closeRect.anchorMax = closeRect.pivot = Vector2.one;
                closeRect.anchoredPosition = _closeInset;
                closeRect.SetAsLastSibling();
            }
            _view.InteriorChanged += OnInteriorChanged;
            ResolveServices();
            Hide();
            ScreenTicker.Add(Tick);
            if (NpcHomesDemoDriver.Stage != null) NpcHomesDemoDriver.StageReady += OnDemoStageReady;
        }

        private void OnDemoStageReady(string firstNpcId) => HandleDemoStage(NpcHomesDemoDriver.Stage, firstNpcId);

        /// <summary>로직 서비스와 클릭 원천(InteriorNpcWalker가 Ready 때 등록)을 늦게라도 얻는다.</summary>
        private void ResolveServices()
        {
            if (_clicks == null)
            {
                _clicks = GameServices.Get<INpcClickSource>();
                if (_clicks != null) _clicks.NpcClicked += OnNpcClicked;
            }
            if (_interaction == null)
            {
                _interaction = GameServices.Get<INpcInteraction>();
                if (_interaction != null) _interaction.Interacted += OnInteracted;
            }
            if (_homes == null)
            {
                _homes = GameServices.Get<INpcHomes>();
                if (_homes != null) _homes.HomeChanged += OnHomeChanged;
            }
        }

        // ----- 상태 전환 -----

        private void OnNpcClicked(string hubId, string npcId) => Open(hubId, npcId);

        /// <summary>(hubId, npcId) 대화를 연다. 이미 열려 있으면 그 NPC로 바꾼다. 상호작용 서비스가 없으면 한 번 기록하고 무시한다.</summary>
        public void Open(string hubId, string npcId)
        {
            ResolveServices();
            if (_interaction == null)
            {
                if (!_loggedMissing) Debug.Log("NpcDialogScreen: INpcInteraction 미등록 - 대화 화면을 열지 않습니다.");
                _loggedMissing = true;
                return;
            }
            if (string.IsNullOrEmpty(hubId) || string.IsNullOrEmpty(npcId)) return;
            _hub = hubId;
            _npc = npcId;
            _typedLine = null;
            SetPortrait(npcId);
            _screens.Show(_screen.Id);
            SetResult(null, true);
            Refresh();
            InteriorNpcWalker.SetHighlighted(_npc);
        }

        /// <summary>대화를 닫는다(닫기 버튼·Esc).</summary>
        public void Close() => Hide();

        private void Hide()
        {
            bool wasOpen = _npc != null;
            _hub = null;
            _npc = null;
            if (_screens != null) _screens.Hide(_screen.Id);
            if (wasOpen) InteriorNpcWalker.SetHighlighted(null);
        }

        private void OnInteriorChanged(bool open)
        {
            if (!open || _view.InteriorHubId != _hub) Hide();
        }

        private void OnInteracted(string npcId, string optionId) { if (npcId == _npc) Refresh(); }
        private void OnHomeChanged(string npcId) { if (npcId == _npc) Refresh(); }

        // ----- 그리기 -----

        private void Refresh()
        {
            if (_npc == null || _interaction == null) return;
            _title.text = _interaction.DisplayName(_npc);
            string habitat = HomeHabitatName(_npc);
            _home.text = habitat != null ? Strings.Get("npc_dialog_home") + "  " + habitat : Strings.Get("npc_dialog_home_none");
            _home.color = habitat != null ? _theme.Muted : _theme.Accent;
            if (_origin != null)
            {
                string origin = _interaction.Origin(_npc);
                bool known = !string.IsNullOrEmpty(origin);
                _origin.text = known ? string.Format(Strings.Get("npc_dialog_origin"), origin) : string.Empty;
                if (_origin.gameObject.activeSelf != known) _origin.gameObject.SetActive(known);
            }
            SetLine(_interaction.CurrentLine(_npc));
            BuildOptions();
        }

        private string HomeHabitatName(string npcId)
        {
            NpcHome home = _homes?.HomeOf(npcId);
            if (home == null || string.IsNullOrEmpty(home.HabitatTypeId) || home.Cells == null || home.Cells.Count == 0) return null;
            try { return Strings.Get("habitat_type_" + home.HabitatTypeId); }
            catch (KeyNotFoundException) { return home.HabitatTypeId; }
        }

        /// <summary>Options(npc) 데이터로 버튼을 다시 만든다(프리팹 예시 버튼은 지운다). 비활성 선택지는 아래 줄에 이유를 보인다.</summary>
        private void BuildOptions()
        {
            for (int i = _options.childCount - 1; i >= 0; i--)
            {
                GameObject child = _options.GetChild(i).gameObject;
                child.SetActive(false);
                Destroy(child);
            }
            var reasons = new StringBuilder();
            IReadOnlyList<NpcInteractionOption> options = _interaction.Options(_npc);
            if (options != null)
                foreach (NpcInteractionOption option in options)
                {
                    if (option == null || string.IsNullOrEmpty(option.Id)) continue;
                    string id = option.Id;
                    DialogOptionView view = Instantiate(_optionPrefab, _options);
                    view.name = "Option_" + id;
                    view.Show(option.Label ?? id, option.Enabled);
                    view.Button.onClick.AddListener(() => Press(id));
                    if (!option.Enabled && !string.IsNullOrEmpty(option.Reason))
                    {
                        if (reasons.Length > 0) reasons.Append('\n');
                        reasons.Append(option.Label ?? id).Append(": ").Append(option.Reason);
                    }
                }
            _reasons.text = reasons.ToString();
            _reasons.gameObject.SetActive(reasons.Length > 0);
        }

        private bool Press(string optionId)
        {
            if (_npc == null || _interaction == null) return false;
            bool ok = _interaction.TryInteract(_npc, optionId, out string resultText);
            SetResult(resultText, ok);
            Refresh();
            return ok;
        }

        private void SetResult(string text, bool ok)
        {
            _result.text = text ?? string.Empty;
            _result.color = ok ? _theme.Text : _theme.Accent;
            _result.gameObject.SetActive(!string.IsNullOrEmpty(text));
        }

        /// <summary>
        /// 데모 단계(스토리 017 캡처). "panel": hub-1의 npcId(없으면 첫 합류 NPC) 대화를 연다. "encouraged": 같은 대화를 열고 격려가 가능하면
        /// 버튼과 같은 경로로 누른다(이미 했으면 비활성 이유를 결과 줄에). 그 밖의 단계는 닫는다.
        /// </summary>
        public static void HandleDemoStage(string stage, string npcId = null)
        {
            if (_instance == null) return;
            if (stage != "panel" && stage != "encouraged") { _instance.Hide(); return; }
            if (string.IsNullOrEmpty(npcId))
            {
                IReadOnlyList<string> npcs = GameServices.Get<INpcRecruiter>()?.NpcsAt(DEMO_HUB);
                npcId = npcs != null && npcs.Count > 0 ? npcs[0] : null;
            }
            if (npcId == null)
            {
                Debug.LogWarning("NpcDialogScreen: 데모 단계 " + stage + " - " + DEMO_HUB + "에 합류한 NPC가 없습니다.");
                return;
            }
            _instance.Open(DEMO_HUB, npcId);
            if (stage != "encouraged" || _instance._npc == null) return;
            NpcInteractionOption encourage = null;
            foreach (NpcInteractionOption option in _instance._interaction.Options(_instance._npc))
                if (option != null && option.Id == ENCOURAGE_OPTION) encourage = option;
            if (encourage == null) return;
            if (encourage.Enabled) _instance.Press(ENCOURAGE_OPTION);
            else _instance.SetResult(encourage.Reason, false);
        }

        /// <summary>화면이 꺼져 있어도 도는 일(ScreenTicker): 늦은 서비스 연결, 열린 동안 Esc·거점 이탈로 닫기.</summary>
        private void Tick()
        {
            if (_clicks == null || _interaction == null || _homes == null) ResolveServices();
            if (_npc == null) return;
            if (!_view.InteriorOpen || _view.InteriorHubId != _hub) { Hide(); return; }
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) Hide();
            if (_npc != null) TickTyping();
        }

        /// <summary>스토리 027: 대사가 바뀌었을 때만 타자기를 처음부터 시작한다(같은 대사로 다시 그리면 그대로 둔다).</summary>
        private void SetLine(string line)
        {
            line = line ?? string.Empty;
            if (line == _typedLine) return;
            _typedLine = line;
            _line.text = line;
            _typing = _typeSpeed > 0f && line.Length > 0;
            _typed = 0f;
            _line.maxVisibleCharacters = _typing ? 0 : int.MaxValue;
        }

        /// <summary>스토리 027: 초당 _typeSpeed 글자씩 드러내고, 대화 상자 위 왼쪽 클릭이면 즉시 완성한다.</summary>
        private void TickTyping()
        {
            if (!_typing || _line == null) return;
            int total = Mathf.Max(1, _line.textInfo != null && _line.textInfo.characterCount > 0 ? _line.textInfo.characterCount : _typedLine.Length);
            _typed += _typeSpeed * Time.unscaledDeltaTime;
            Mouse mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame && _panel != null)
            {
                Canvas canvas = _panel.GetComponentInParent<Canvas>();
                Camera eye = canvas != null && canvas.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.rootCanvas.worldCamera : null;
                if (RectTransformUtility.RectangleContainsScreenPoint(_panel, mouse.position.ReadValue(), eye)) _typed = total;
            }
            if (_typed >= total) { _typing = false; _line.maxVisibleCharacters = int.MaxValue; return; }
            _line.maxVisibleCharacters = (int)_typed;
        }

        /// <summary>스토리 027: 초상 상자에 NPC 몸체 스프라이트를 넣는다(없으면 숨김).</summary>
        private void SetPortrait(string npcId)
        {
            if (_portrait == null) return;
            Sprite sprite = InteriorNpcWalker.BodySprite(npcId);
            _portrait.sprite = sprite;
            _portrait.preserveAspect = true;
            _portrait.enabled = sprite != null;
        }

        void OnDestroy()
        {
            GameServices.Ready -= Create;
            if (_instance != this) return;
            ScreenTicker.Remove(Tick);
            NpcHomesDemoDriver.StageReady -= OnDemoStageReady;
            if (_view != null) _view.InteriorChanged -= OnInteriorChanged;
            if (_clicks != null) _clicks.NpcClicked -= OnNpcClicked;
            if (_interaction != null) _interaction.Interacted -= OnInteracted;
            if (_homes != null) _homes.HomeChanged -= OnHomeChanged;
            _instance = null;
        }
    }
}
