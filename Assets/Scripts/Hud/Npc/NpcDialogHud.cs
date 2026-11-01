using System;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

using IronRing.Contracts;
using IronRing.Contracts.Presentation;
using IronRing.Localization;
using IronRing.Npc.Demo;
using IronRing.Presentation.HubPhase;
using IronRing.Presentation.Interior;

namespace IronRing.Hud.Npc
{
    /// <summary>
    /// 스토리 017. 거점 내부 NPC 대화 패널(UI Toolkit, 왼쪽 아래 핫바 위). INpcClickSource.NpcClicked로 열리고,
    /// 직업명(INpcInteraction.DisplayName), 집 서식지 이름(INpcHomes, 없으면 "집 없음"), 현재 대사, 결과 한 줄,
    /// 선택지 버튼(INpcInteraction.Options 데이터, 비활성이면 이유 표시)과 [닫기]를 보인다.
    /// 상태: 숨김 / 열림(hub, npc). 닫기 버튼·Esc·내부 화면 닫힘·다른 거점이면 숨김, 다른 NPC 클릭이면 그 NPC로 전환.
    /// 게임 상태는 바꾸지 않고 TryInteract 명령만 보낸다. 패널 영역은 PlacementGhost.UiHitTests에 등록한다.
    /// </summary>
    public sealed class NpcDialogHud : MonoBehaviour
    {
        /// <summary>핫바(120) 위에 그린다.</summary>
        private const int SORTING_ORDER = 125;
        private const string DEMO_HUB = "hub-1";
        private const string ENCOURAGE_OPTION = "encourage";

        private static NpcDialogHud _instance;
        private static bool _loggedMissing;

        /// <summary>현재 패널(데모용). 없으면 null.</summary>
        public static NpcDialogHud Instance => _instance;
        /// <summary>데모 브리지: (hubId, npcId) 패널을 연다. HUD가 없으면 null.</summary>
        public static Action<string, string> OpenForDemo;
        /// <summary>데모 브리지: 열린 패널에서 선택지 id를 버튼과 같은 경로로 누른다. 실행 성공이면 true.</summary>
        public static Func<string, bool> InteractForDemo;

        private IViewMode _view;
        private INpcInteraction _interaction;
        private INpcHomes _homes;
        private INpcClickSource _clicks;

        private VisualElement _root;
        private VisualElement _panel;
        private Label _title;
        private Label _home;
        private Label _line;
        private Label _result;
        private VisualElement _options;
        private VisualElement _reasons;

        private string _hub;
        private string _npc;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            GameServices.Ready -= Create;
            _instance = null;
            _loggedMissing = false;
            OpenForDemo = null;
            InteractForDemo = null;
        }

        /// <summary>MainRoot의 GameServices.Reset()(SubsystemRegistration) 뒤에 Ready를 구독한다.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Boot() { GameServices.Ready -= Create; GameServices.Ready += Create; }

        /// <summary>내부 화면(IViewMode)이 있는 씬에서 한 번 만든다. 씬 편집 없이 Main.unity 플레이에 나타난다.</summary>
        private static void Create()
        {
            if (_instance != null || GameServices.Get<IViewMode>() == null) return;
            _instance = new GameObject(nameof(NpcDialogHud)).AddComponent<NpcDialogHud>();
            _instance.Initialize();
        }

        private void Initialize()
        {
            _view = GameServices.Get<IViewMode>();
            var document = gameObject.AddComponent<UIDocument>();
            document.panelSettings = KoreanUi.DefaultPanel;
            document.sortingOrder = SORTING_ORDER;
            document.visualTreeAsset = UnityEngine.Resources.Load<VisualTreeAsset>("Hud/NpcDialog");
            _root = document.rootVisualElement;
            _root.pickingMode = PickingMode.Ignore;
            foreach (TemplateContainer container in _root.Query<TemplateContainer>().ToList()) container.pickingMode = PickingMode.Ignore;
            // 다른 HUD와 같은 이유로 USS를 UXML과 다른 이름으로 두고 직접 붙인다.
            StyleSheet style = UnityEngine.Resources.Load<StyleSheet>("Hud/NpcDialogStyle");
            if (style == null) UnityEngine.Debug.LogError("NpcDialogHud: Resources/Hud/NpcDialogStyle.uss를 불러오지 못했습니다.");
            else if (!_root.styleSheets.Contains(style)) _root.styleSheets.Add(style);
            KoreanUi.Apply(_root);

            _root.Q("npc-dialog-root").pickingMode = PickingMode.Ignore;
            _panel = _root.Q("npc-dialog");
            _title = _root.Q<Label>("npc-dialog-title");
            _home = _root.Q<Label>("npc-dialog-home");
            _line = _root.Q<Label>("npc-dialog-line");
            _result = _root.Q<Label>("npc-dialog-result");
            _options = _root.Q("npc-dialog-options");
            _reasons = _root.Q("npc-dialog-reasons");
            Button close = _root.Q<Button>("npc-dialog-close");
            close.text = Strings.Get("npc_dialog_close");
            close.clicked += Close;

            PlacementGhost.UiHitTests.Add(IsOverPanel);
            _view.InteriorChanged += OnInteriorChanged;
            OpenForDemo = Open;
            InteractForDemo = Press;
            ResolveServices();
            Hide();
            if (NpcHomesDemoDriver.Stage != null) NpcHomesDemoDriver.StageReady += OnDemoStageReady;
        }

        /// <summary>NpcHomesDemoDriver가 합류(와 encouraged면 격려)를 마친 뒤: 단계에 맞게 패널을 연다.</summary>
        private void OnDemoStageReady(string firstNpcId) => HandleDemoStage(NpcHomesDemoDriver.Stage, firstNpcId);

        /// <summary>로직 쪽 서비스(INpcInteraction·INpcHomes)와 클릭 원천(InteriorNpcWalker가 Ready 때 등록)을 늦게라도 얻는다.</summary>
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

        /// <summary>(hubId, npcId) 패널을 연다. 이미 열려 있으면 그 NPC로 전환한다. 상호작용 서비스가 없으면 한 번 기록하고 무시한다.</summary>
        public void Open(string hubId, string npcId)
        {
            ResolveServices();
            if (_interaction == null)
            {
                if (!_loggedMissing) UnityEngine.Debug.Log("NpcDialogHud: INpcInteraction 미등록 - 대화 패널을 열지 않습니다.");
                _loggedMissing = true;
                return;
            }
            if (string.IsNullOrEmpty(hubId) || string.IsNullOrEmpty(npcId)) return;
            _hub = hubId;
            _npc = npcId;
            SetResult(null, true);
            Refresh();
            _panel.style.display = DisplayStyle.Flex;
            InteriorNpcWalker.SetHighlighted(_npc);
        }

        /// <summary>패널을 닫는다(닫기 버튼·Esc).</summary>
        public void Close() => Hide();

        private void Hide()
        {
            bool wasOpen = _npc != null;
            _hub = null;
            _npc = null;
            if (_panel != null) _panel.style.display = DisplayStyle.None;
            if (wasOpen) InteriorNpcWalker.SetHighlighted(null);
        }

        private void OnInteriorChanged(bool open)
        {
            if (!open || _view.InteriorHubId != _hub) Hide();
        }

        private void OnInteracted(string npcId, string optionId) { if (npcId == _npc) Refresh(); }
        private void OnHomeChanged(string npcId) { if (npcId == _npc) Refresh(); }

        // ----- 화면 갱신 -----

        /// <summary>열린 NPC의 제목·집·대사·선택지를 서비스에서 다시 읽는다.</summary>
        private void Refresh()
        {
            if (_npc == null || _interaction == null) return;
            _title.text = _interaction.DisplayName(_npc);
            string habitat = HomeHabitatName(_npc);
            _home.text = habitat != null ? Strings.Get("npc_dialog_home") + "  " + habitat : Strings.Get("npc_dialog_home_none");
            _home.EnableInClassList("npc-dialog-home--none", habitat == null);
            _line.text = _interaction.CurrentLine(_npc);
            BuildOptions();
        }

        /// <summary>집 서식지 이름(현지화). 집이 없거나 칸이 비었으면(깨짐) null.</summary>
        private string HomeHabitatName(string npcId)
        {
            NpcHome home = _homes?.HomeOf(npcId);
            if (home == null || string.IsNullOrEmpty(home.HabitatTypeId) || home.Cells == null || home.Cells.Count == 0) return null;
            try { return Strings.Get("habitat_type_" + home.HabitatTypeId); }
            catch (KeyNotFoundException) { return home.HabitatTypeId; }
        }

        /// <summary>Options(npc) 데이터로 버튼을 다시 만든다. 비활성 선택지는 이유를 툴팁과 아래 줄에 보인다.</summary>
        private void BuildOptions()
        {
            _options.Clear();
            _reasons.Clear();
            IReadOnlyList<NpcInteractionOption> options = _interaction.Options(_npc);
            if (options == null) return;
            foreach (NpcInteractionOption option in options)
            {
                if (option == null || string.IsNullOrEmpty(option.Id)) continue;
                string id = option.Id;
                var button = new Button(() => Press(id)) { text = option.Label ?? id, name = "npc-dialog-option-" + id };
                button.AddToClassList("npc-dialog-option");
                button.SetEnabled(option.Enabled);
                if (!option.Enabled && !string.IsNullOrEmpty(option.Reason))
                {
                    button.tooltip = option.Reason;
                    var reason = new Label((option.Label ?? id) + ": " + option.Reason);
                    reason.AddToClassList("npc-dialog-reason");
                    _reasons.Add(reason);
                }
                _options.Add(button);
            }
        }

        /// <summary>선택지 실행: TryInteract 결과 문장을 한 줄로 보이고 대사·버튼을 다시 읽는다.</summary>
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
            if (_result == null) return;
            _result.text = text ?? string.Empty;
            _result.EnableInClassList("npc-dialog-result--fail", !ok);
            _result.style.display = string.IsNullOrEmpty(text) ? DisplayStyle.None : DisplayStyle.Flex;
        }

        /// <summary>
        /// 데모 단계 처리(스토리 017 캡처). "panel": hub-1의 npcId(없으면 첫 합류 NPC) 패널을 연다. "encouraged": 같은 패널을 열고,
        /// 격려가 아직 가능하면 버튼과 같은 경로로 눌러 결과 문장과 비활성 버튼을 보인다(이미 격려했으면 비활성 이유를 결과 줄에 보인다).
        /// 그 밖의 단계(예: "homes")는 패널을 닫는다.
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
                UnityEngine.Debug.LogWarning("NpcDialogHud: 데모 단계 " + stage + " - " + DEMO_HUB + "에 합류한 NPC가 없습니다.");
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

        // ----- 입력 -----

        /// <summary>화면 좌표가 열린 패널 위인지(월드 클릭 무시용).</summary>
        private bool IsOverPanel(Vector2 screen)
        {
            if (_npc == null || _panel?.panel == null) return false;
            Vector2 point = RuntimePanelUtils.ScreenToPanel(_panel.panel, new Vector2(screen.x, Screen.height - screen.y));
            return _panel.worldBound.Contains(point);
        }

        void Update()
        {
            if (_clicks == null || _interaction == null || _homes == null) ResolveServices();
            if (_npc == null) return;
            if (!_view.InteriorOpen || _view.InteriorHubId != _hub) { Hide(); return; }
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) Hide();
        }

        void OnDestroy()
        {
            GameServices.Ready -= Create;
            NpcHomesDemoDriver.StageReady -= OnDemoStageReady;
            PlacementGhost.UiHitTests.Remove(IsOverPanel);
            if (_view != null) _view.InteriorChanged -= OnInteriorChanged;
            if (_clicks != null) _clicks.NpcClicked -= OnNpcClicked;
            if (_interaction != null) _interaction.Interacted -= OnInteracted;
            if (_homes != null) _homes.HomeChanged -= OnHomeChanged;
            if (_instance == this)
            {
                _instance = null;
                OpenForDemo = null;
                InteractForDemo = null;
            }
        }
    }
}
