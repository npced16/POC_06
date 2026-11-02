using System.Collections.Generic;
using System.Text;

using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

using TMPro;

using IronRing.Contracts;
using IronRing.Contracts.Presentation;
using IronRing.Hud.Hotbar;
using IronRing.Hud.Ui;
using IronRing.Localization;
using IronRing.Npc;
using IronRing.Npc.Demo;
using IronRing.Presentation.HubPhase;

namespace IronRing.Hud.Npc
{
    /// <summary>
    /// 스토리 029: 옛 온라인 게임식 대화 장면(상황 화면 "NpcDialog"). 화면 전체를 반투명 그늘(_dim, 클릭 = 닫기)로 덮고, 왼쪽 아래에 큰 상반신 초상
    /// (NpcPortraitCatalog, 없으면 몸체 스프라이트 ×_fallbackPortraitScale), 아래 가운데에 이중 테두리 대화 상자(이름·집/출신·대사·버튼 줄)를 둔다.
    /// 상태: 닫힘 → 읽기(대사 순서 = NPC의 _quote + _lines, [계속하기]: 타자기 중이면 즉시 완성, 아니면 다음 줄) → 선택(마지막 줄 뒤
    /// [계속하기] 대신 INpcInteraction.Options 버튼 + [닫기]; 결과 문장은 대사 칸에 타자기로). Esc·그늘 클릭·닫기·내부 화면 닫힘·다른 거점이면 닫는다.
    /// 긴 대사(QA 2026-11-01 "4줄 초과 대사"): 대사 칸은 TMP Overflow=Page. 한 대사가 칸(빌더 BODY_LINES줄)을 넘으면 쪽으로 나뉘어
    /// 타자기가 1쪽부터 치고, [계속하기] 자리에 "▼ 계속"(ui_npcdialog_more)이 떠 다음 쪽으로 넘긴다. 마지막 쪽에서만 다음 대사·선택지로 간다.
    /// 선택 단계의 결과 문장이 넘쳐도 같은 버튼이 잠시 떠 쪽을 넘긴다. 말줄임(…)·잘림은 쓰지 않는다.
    /// 게임 상태는 바꾸지 않고 TryInteract 명령만 보낸다. 데모 단계(NpcHomesDemoDriver.StageReady)는 HandleDemoStage가 처리한다.
    /// </summary>
    [RequireComponent(typeof(UiScreen))]
    public sealed class NpcDialogScreen : MonoBehaviour
    {
        private const string DEMO_HUB = "hub-1";
        private const string ENCOURAGE_OPTION = "encourage";
        private const string PORTRAIT_CATALOG = "Npc/NpcPortraits";
        private static NpcDialogScreen _instance;
        private static bool _loggedMissing;

        /// <summary>현재 대화 화면(데모용). 초기화 전이면 null.</summary>
        public static NpcDialogScreen Instance => _instance;

        [Tooltip("이름(Heading, 황동색).")]
        [SerializeField] private TMP_Text _title;
        [Tooltip("이름 줄 오른쪽의 집·출신지(Small, 흐린 색).")]
        [SerializeField] private TMP_Text _meta;
        [Tooltip("대사 영역(Normal, 한 쪽 최대 4줄, Overflow=Page, 타자기).")]
        [SerializeField] private TMP_Text _line;
        [Tooltip("비활성 선택지 이유(Small, 버튼 줄 왼쪽).")]
        [SerializeField] private TMP_Text _reasons;
        [SerializeField] private RectTransform _options;
        [Tooltip("선택지 버튼 부품(Parts/DialogOption.prefab).")]
        [SerializeField] private DialogOptionView _optionPrefab;
        [Tooltip("선택 단계의 [닫기].")]
        [SerializeField] private Button _close;
        [Tooltip("읽기 단계의 [계속하기].")]
        [SerializeField] private Button _continue;
        [Tooltip("전체 화면 그늘(클릭 = 닫기, 배치 입력 차단).")]
        [SerializeField] private Image _dim;
        [Tooltip("대화 상자(대사 위 클릭 = 타자기 즉시 완성 영역).")]
        [SerializeField] private RectTransform _panel;
        [Tooltip("왼쪽 아래 큰 초상(아래 기준, 높이 고정·폭은 비율).")]
        [SerializeField] private Image _portrait;
        [Tooltip("그늘 알파(0~1). 데이터 값.")]
        [SerializeField] private float _dialogDimAlpha = 0.35f;
        [Tooltip("초상 카탈로그에 없을 때 몸체 스프라이트 확대 배수.")]
        [SerializeField] private float _fallbackPortraitScale = 4f;
        [Tooltip("타자기 속도(초당 글자). 0이면 한 번에 보인다.")]
        [SerializeField] private float _typeSpeed = 28f;
        [Tooltip("초상이 상자 왼쪽을 덮는 폭에 더하는 글자 여백(px). 이름·집/출신·대사·이유 줄의 왼쪽 시작 = max(_textMinLeft, 겹침 폭 + 이 값).")]
        [SerializeField] private float _portraitTextGap = 16f;
        [Tooltip("초상이 없거나 겹치지 않을 때 글자 줄 왼쪽 최소 여백(px, 패널 안쪽).")]
        [SerializeField] private float _textMinLeft = 24f;
        [Tooltip("글자 줄 오른쪽 여백(px, 상자 오른쪽 가장자리에서). 이름·집/출신·대사는 오른쪽이 상자에 늘어나 붙는다.")]
        [SerializeField] private float _textRightPad = 24f;

        private UiScreens _screens;
        private UiScreen _screen;
        private UiTheme _theme;
        private IViewMode _view;
        private INpcInteraction _interaction;
        private INpcHomes _homes;
        private INpcClickSource _clicks;
        private NpcPortraitCatalog _portraits;
        private float _portraitHeight;
        private float _textLeft = -1f;
        private readonly Vector3[] _corners = new Vector3[4];
        private string _hub;
        private string _npc;
        private readonly List<string> _sequence = new List<string>();
        private int _index;
        private bool _choosing;
        private string _typedLine;
        private float _typed;
        private bool _typing;
        private int _page = 1;
        private bool _labelMore;

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
            if (placed == null || !placed.enabled || placed._optionPrefab == null || placed._options == null || placed._continue == null) return;
            _instance = placed;
            placed.Initialize(screens);
        }

        private void Initialize(UiScreens screens)
        {
            _screens = screens;
            _screen = GetComponent<UiScreen>();
            _theme = screens.Theme;
            _view = GameServices.Get<IViewMode>();
            _portraits = Resources.Load<NpcPortraitCatalog>(PORTRAIT_CATALOG);
            if (_portrait != null) _portraitHeight = _portrait.rectTransform.sizeDelta.y;
            SetLabel(_close, "npc_dialog_close");
            SetLabel(_continue, "npc_dialog_continue");
            _line.overflowMode = TextOverflowModes.Page; // 프리팹이 옛 Truncate여도 잘리지 않게(빌더도 Page로 만든다)
            _close.onClick.AddListener(Close);
            _continue.onClick.AddListener(Continue);
            if (_dim != null)
            {
                Color shade = _dim.color; shade.a = Mathf.Clamp01(_dialogDimAlpha); _dim.color = shade;
                Button dimButton = _dim.GetComponent<Button>();
                if (dimButton != null) dimButton.onClick.AddListener(Close);
            }
            _view.InteriorChanged += OnInteriorChanged;
            ResolveServices();
            Hide();
            ScreenTicker.Add(Tick);
            if (NpcHomesDemoDriver.Stage != null) NpcHomesDemoDriver.StageReady += OnDemoStageReady;
        }

        private static void SetLabel(Button button, string key)
        {
            TMP_Text label = button != null ? button.GetComponentInChildren<TMP_Text>(true) : null;
            if (label != null) label.text = Strings.Get(key);
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

        /// <summary>(hubId, npcId) 대화를 처음 줄부터 연다. 이미 열려 있으면 그 NPC로 바꾼다. 상호작용 서비스가 없으면 한 번 기록하고 무시한다.</summary>
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
            if (_npc != null && _npc != npcId) InteriorNpcWalker.SetTalking(_npc, false);
            _hub = hubId;
            _npc = npcId;
            BuildSequence(npcId);
            _index = 0;
            _choosing = false;
            _typedLine = null;
            SetPortrait(npcId);
            _screens.Show(_screen.Id);
            RefreshHeader();
            LayoutText(); // 쪽 나눔이 실제 글자 폭으로 계산되도록 대사보다 먼저
            ShowStep();
            InteriorNpcWalker.SetHighlighted(_npc);
            InteriorNpcWalker.SetTalking(_npc, true);
            HotbarScreen.Instance?.SetSuppressed(true);
        }

        /// <summary>대화를 닫는다(닫기 버튼·Esc·그늘 클릭).</summary>
        public void Close() => Hide();

        /// <summary>[계속하기]: 타자기 중이면 지금 쪽을 즉시 완성, 남은 쪽이 있으면 다음 쪽, 아니면 다음 줄, 마지막 줄 뒤에는 선택 단계로.</summary>
        public void Continue()
        {
            if (_npc == null) return;
            if (_typing) { FinishTyping(); return; }
            if (HasMorePages()) { ShowPage(_page + 1); return; }
            if (_choosing) return;
            if (_index < _sequence.Count - 1) { _index++; ShowStep(); return; }
            _choosing = true;
            ShowStep();
        }

        private void Hide()
        {
            string was = _npc;
            _hub = null;
            _npc = null;
            _choosing = false;
            if (_screens != null) _screens.Hide(_screen.Id);
            if (was != null) { InteriorNpcWalker.SetHighlighted(null); InteriorNpcWalker.SetTalking(was, false); HotbarScreen.Instance?.SetSuppressed(false); }
        }

        private void OnInteriorChanged(bool open)
        {
            if (!open || _view.InteriorHubId != _hub) Hide();
        }

        private void OnInteracted(string npcId, string optionId) { if (npcId == _npc && _choosing) BuildOptions(); }
        private void OnHomeChanged(string npcId) { if (npcId == _npc) RefreshHeader(); }

        /// <summary>대사 순서 = 풀 정의의 _quote(있으면) + _lines. 정의가 없으면 INpcInteraction.CurrentLine 한 줄.</summary>
        private void BuildSequence(string npcId)
        {
            _sequence.Clear();
            NpcDefinition definition = NpcInstaller.Pool?.Find(npcId);
            if (definition != null)
            {
                if (!string.IsNullOrEmpty(definition.Quote)) _sequence.Add(definition.Quote);
                if (definition.Lines != null)
                    foreach (string line in definition.Lines)
                        if (!string.IsNullOrEmpty(line)) _sequence.Add(line);
            }
            if (_sequence.Count == 0) _sequence.Add(_interaction.CurrentLine(npcId) ?? string.Empty);
        }

        // ----- 그리기 -----

        private void RefreshHeader()
        {
            if (_npc == null || _interaction == null) return;
            _title.text = _interaction.DisplayName(_npc);
            if (_meta == null) return;
            string habitat = HomeHabitatName(_npc);
            var meta = new StringBuilder(habitat != null ? Strings.Get("npc_dialog_home") + "  " + habitat : Strings.Get("npc_dialog_home_none"));
            string origin = _interaction.Origin(_npc);
            if (!string.IsNullOrEmpty(origin)) meta.Append("  ·  ").Append(string.Format(Strings.Get("npc_dialog_origin"), origin));
            _meta.text = meta.ToString();
            _meta.color = habitat != null ? _theme.Muted : _theme.Accent;
        }

        /// <summary>읽기 단계면 _index 줄과 [계속하기], 선택 단계면 선택지 + [닫기](대사 칸은 마지막 줄 그대로).</summary>
        private void ShowStep()
        {
            _options.gameObject.SetActive(_choosing);
            _close.gameObject.SetActive(_choosing);
            if (_choosing) BuildOptions();
            else
            {
                SetLine(_sequence[Mathf.Clamp(_index, 0, _sequence.Count - 1)], true);
                _reasons.gameObject.SetActive(false);
            }
            RefreshContinue();
        }

        private string HomeHabitatName(string npcId)
        {
            NpcHome home = _homes?.HomeOf(npcId);
            if (home == null || string.IsNullOrEmpty(home.HabitatTypeId) || home.Cells == null || home.Cells.Count == 0) return null;
            try { return Strings.Get("habitat_type_" + home.HabitatTypeId); }
            catch (KeyNotFoundException) { return home.HabitatTypeId; }
        }

        /// <summary>Options(npc) 데이터로 버튼을 다시 만든다(프리팹 예시 버튼은 지운다). 비활성 선택지는 버튼 줄 왼쪽에 이유를 보인다.</summary>
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
                        if (reasons.Length > 0) reasons.Append("  ");
                        reasons.Append(option.Label ?? id).Append(": ").Append(option.Reason);
                    }
                }
            _reasons.text = reasons.ToString();
            _reasons.gameObject.SetActive(reasons.Length > 0);
        }

        /// <summary>선택지를 실행하고 결과 문장(대화면 다음 대사)을 대사 칸에 한 줄로 보인다.</summary>
        private bool Press(string optionId)
        {
            if (_npc == null || _interaction == null) return false;
            bool ok = _interaction.TryInteract(_npc, optionId, out string resultText);
            if (!string.IsNullOrEmpty(resultText)) SetLine(resultText, ok);
            if (_npc != null && _choosing) BuildOptions();
            return ok;
        }

        /// <summary>
        /// 데모 단계(스토리 017·029 캡처). "panel": hub-1의 npcId(없으면 첫 합류 NPC) 대화를 연다. "panel-end": 열고 [계속하기]를 끝까지 눌러
        /// 선택지 줄을 보인다. "encouraged": panel-end 뒤 격려가 가능하면 버튼과 같은 경로로 누른다(이미 했으면 이유를 대사 칸에). 그 밖의 단계는 닫는다.
        /// </summary>
        public static void HandleDemoStage(string stage, string npcId = null)
        {
            if (_instance == null) return;
            if (stage != "panel" && stage != "panel-end" && stage != "encouraged") { _instance.Hide(); return; }
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
            if (stage == "panel" || _instance._npc == null) return;
            for (int guard = 0; guard < 64 && !_instance._choosing; guard++) _instance.Continue();
            _instance.FinishTyping();
            Debug.Log($"NpcDialogScreen: 데모 {stage} - 대사 {_instance._sequence.Count}줄 끝, 선택지 줄 표시");
            if (stage != "encouraged") return;
            NpcInteractionOption encourage = null;
            foreach (NpcInteractionOption option in _instance._interaction.Options(_instance._npc))
                if (option != null && option.Id == ENCOURAGE_OPTION) encourage = option;
            if (encourage == null) return;
            if (encourage.Enabled) _instance.Press(ENCOURAGE_OPTION);
            else _instance.SetLine(encourage.Reason, false);
        }

        /// <summary>화면이 꺼져 있어도 도는 일(ScreenTicker): 늦은 서비스 연결, 열린 동안 Esc·거점 이탈로 닫기, 타자기.</summary>
        private void Tick()
        {
            if (_clicks == null || _interaction == null || _homes == null) ResolveServices();
            if (_npc == null) return;
            if (!_view.InteriorOpen || _view.InteriorHubId != _hub) { Hide(); return; }
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) { Hide(); return; }
            LayoutText();
            TickTyping();
            RefreshContinue();
        }

        /// <summary>
        /// 초상이 상자 왼쪽을 덮는 폭(초상 오른쪽 가장자리 − 상자 왼쪽 가장자리, 상자 로컬 좌표)을 재서 글자 줄들의 왼쪽 시작을
        /// max(_textMinLeft, 겹침 + _portraitTextGap)으로 둔다. 화면 비율·초상 폭이 바뀌면 따라간다(값이 같으면 건드리지 않음).
        /// </summary>
        private void LayoutText()
        {
            if (_panel == null || _line == null) return;
            float left = _textMinLeft;
            if (_portrait != null && _portrait.enabled && _portrait.gameObject.activeInHierarchy)
            {
                _portrait.rectTransform.GetWorldCorners(_corners);
                float portraitRight = _panel.InverseTransformPoint(_corners[2]).x;
                float panelLeft = _panel.rect.xMin;
                left = Mathf.Max(left, portraitRight - panelLeft + _portraitTextGap);
            }
            left = Mathf.Round(left);
            if (Mathf.Approximately(left, _textLeft)) return;
            _textLeft = left;
            SetLeft(_title); SetLeft(_meta); SetLeft(_line);
            if (_reasons != null) { Vector2 at = _reasons.rectTransform.anchoredPosition; at.x = left; _reasons.rectTransform.anchoredPosition = at; }
        }

        private void SetLeft(TMP_Text text)
        {
            if (text == null) return;
            RectTransform rect = text.rectTransform;
            // 가로는 상자 전체로 늘이고(왼쪽 0 ~ 오른쪽 1) 왼쪽 시작만 겹침에 맞춘다. 세로 앵커·여백은 빌더 값을 그대로 둔다.
            rect.anchorMin = new Vector2(0f, rect.anchorMin.y);
            rect.anchorMax = new Vector2(1f, rect.anchorMax.y);
            rect.offsetMin = new Vector2(_textLeft, rect.offsetMin.y);
            rect.offsetMax = new Vector2(-_textRightPad, rect.offsetMax.y);
        }

        /// <summary>대사 칸에 line을 타자기로 시작한다(같은 대사면 그대로). ok가 false면 강조색(실패 이유).</summary>
        private void SetLine(string line, bool ok)
        {
            line = line ?? string.Empty;
            _line.color = ok ? _theme.Text : _theme.Accent;
            if (line == _typedLine) return;
            _typedLine = line;
            _line.text = line;
            _line.pageToDisplay = 1;
            _line.ForceMeshUpdate(true, true); // pageCount·pageInfo를 지금 폭으로 채운다
            ShowPage(1);
        }

        /// <summary>대사 칸의 page쪽(1부터)을 보이고 그 쪽 첫 글자부터 타자기를 시작한다.</summary>
        private void ShowPage(int page)
        {
            _page = Mathf.Clamp(page, 1, PageCount());
            _line.pageToDisplay = _page;
            _typing = _typeSpeed > 0f && !string.IsNullOrEmpty(_typedLine);
            _typed = PageFirst(_page);
            _line.maxVisibleCharacters = _typing ? (int)_typed : int.MaxValue;
            RefreshContinue();
        }

        private int PageCount()
        {
            TMP_TextInfo info = _line != null ? _line.textInfo : null;
            return info != null ? Mathf.Max(1, info.pageCount) : 1;
        }

        private bool HasMorePages() => _line != null && _page < PageCount();

        /// <summary>page쪽 첫 글자의 textInfo 문자 번호(쪽 정보가 없으면 0).</summary>
        private int PageFirst(int page)
        {
            TMP_TextInfo info = _line != null ? _line.textInfo : null;
            if (info == null || info.pageInfo == null || page < 1 || page > info.pageCount || page > info.pageInfo.Length) return 0;
            return Mathf.Max(0, info.pageInfo[page - 1].firstCharacterIndex);
        }

        /// <summary>page쪽 마지막 글자 다음 번호(타자기 끝). 쪽 정보가 없으면 전체 글자 수.</summary>
        private int PageEnd(int page)
        {
            TMP_TextInfo info = _line != null ? _line.textInfo : null;
            int all = info != null && info.characterCount > 0 ? info.characterCount : (_typedLine ?? string.Empty).Length;
            if (info == null || info.pageInfo == null || page < 1 || page > info.pageCount || page > info.pageInfo.Length) return Mathf.Max(1, all);
            return Mathf.Max(1, info.pageInfo[page - 1].lastCharacterIndex + 1);
        }

        /// <summary>[계속하기] 버튼: 읽기 단계면 늘, 선택 단계면 남은 쪽이 있을 때만. 남은 쪽이 있으면 "▼ 계속", 없으면 "계속하기".</summary>
        private void RefreshContinue()
        {
            if (_continue == null) return;
            bool more = HasMorePages();
            bool show = _npc != null && (!_choosing || more);
            if (_continue.gameObject.activeSelf != show) _continue.gameObject.SetActive(show);
            if (more == _labelMore) return;
            _labelMore = more;
            SetLabel(_continue, more ? "ui_npcdialog_more" : "npc_dialog_continue");
        }

        private void FinishTyping()
        {
            _typing = false;
            if (_line != null) _line.maxVisibleCharacters = int.MaxValue; // Page 모드라 지금 쪽만 보인다
        }

        /// <summary>초당 _typeSpeed 글자씩 드러내고, 대화 상자 위 왼쪽 클릭이면 즉시 완성한다.</summary>
        private void TickTyping()
        {
            if (!_typing || _line == null) return;
            int total = PageEnd(_page);
            _typed += _typeSpeed * Time.unscaledDeltaTime;
            Mouse mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame && _panel != null)
            {
                Canvas canvas = _panel.GetComponentInParent<Canvas>();
                Camera eye = canvas != null && canvas.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.rootCanvas.worldCamera : null;
                if (RectTransformUtility.RectangleContainsScreenPoint(_panel, mouse.position.ReadValue(), eye)) _typed = total;
            }
            if (_typed >= total) { FinishTyping(); return; }
            _line.maxVisibleCharacters = (int)_typed;
        }

        /// <summary>초상: 카탈로그 스프라이트면 높이 고정·폭은 비율. 카탈로그가 null을 주면 몸체 스프라이트를 픽셀 크기 × _fallbackPortraitScale로.</summary>
        private void SetPortrait(string npcId)
        {
            if (_portrait == null) return;
            Sprite sprite = _portraits != null ? _portraits.Get(npcId) : null;
            Vector2 size;
            if (sprite != null)
                size = new Vector2(_portraitHeight * sprite.rect.width / Mathf.Max(1f, sprite.rect.height), _portraitHeight);
            else
            {
                sprite = InteriorNpcWalker.BodySprite(npcId);
                size = sprite != null ? sprite.rect.size * _fallbackPortraitScale : Vector2.zero;
            }
            _portrait.sprite = sprite;
            _portrait.preserveAspect = true;
            _portrait.enabled = sprite != null;
            if (sprite != null) _portrait.rectTransform.sizeDelta = size;
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
