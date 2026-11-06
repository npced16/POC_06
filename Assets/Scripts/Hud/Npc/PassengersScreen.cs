using System.Collections.Generic;

using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

using TMPro;

using IronRing.Contracts;
using IronRing.Contracts.Presentation;
using IronRing.Hud.Ui;
using IronRing.Localization;
using IronRing.Npc;
using IronRing.Npc.Demo;
using IronRing.Presentation.HubPhase;

namespace IronRing.Hud.Npc
{
    /// <summary>
    /// 상황 화면 "Passengers": 지금 열차에 탄 승객마다 NPC 그림과 직업 이름(괄호에 서식지)을 한 줄에 보여 준다.
    /// 비어 있으면 "승객이 없다". 상단 바 승객 버튼(Toggle)·P 키로 열고 닫고, 닫기 버튼·Esc로 닫는다. 탑승·하차·이탈 때 다시 그린다.
    /// 숨은 조건과 조합법은 보이지 않는다. 키는 화면이 꺼져 있어도 ScreenTicker로 읽는다.
    /// 스펙 005: 줄을 끌면 NpcDragController로 넘기고 끄는 동안 패널을 감춘다. 게임 상태는 직접 바꾸지 않는다.
    /// </summary>
    [RequireComponent(typeof(UiScreen))]
    public sealed class PassengersScreen : MonoBehaviour
    {
        private const string DEMO_STAGE = "passengers";
        private static PassengersScreen _instance;

        /// <summary>현재 승객 화면. 초기화 전이면 null.</summary>
        public static PassengersScreen Instance => _instance;

        [SerializeField] private TMP_Text _title;
        [SerializeField] private TMP_Text _empty;
        [SerializeField] private Button _close;
        [SerializeField] private RectTransform _list;
        [Tooltip("승객 한 줄 부품(Parts/PassengerRow.prefab).")]
        [SerializeField] private PassengerRowView _rowPrefab;
        [Tooltip("승객을 끄는 동안 패널을 감출 CanvasGroup.")]
        [SerializeField] private CanvasGroup _panelGroup;
        [Tooltip("승객을 끌어 내릴 수 있다는 안내 문구.")]
        [SerializeField] private TMP_Text _hint;

        private UiScreens _screens;
        private UiScreen _screen;
        private IPassengers _passengers;
        private INpcInteraction _interaction;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { GameServices.Ready -= Create; _instance = null; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot() { GameServices.Ready -= Create; GameServices.Ready += Create; }

        /// <summary>승객 서비스가 있고 캔버스에 화면이 있으면 한 번 바인딩한다.</summary>
        private static void Create()
        {
            if (_instance != null || GameServices.Get<IPassengers>() == null) return;
            UiScreens screens = UiScreens.Instance;
            PassengersScreen placed = screens != null ? screens.Find<PassengersScreen>() : null;
            if (placed == null || !placed.enabled || placed._rowPrefab == null || placed._list == null || placed._empty == null) return;
            _instance = placed;
            placed.Initialize(screens);
        }

        /// <summary>승객 화면이 열려 있는지.</summary>
        public static bool IsOpen => _instance != null && _instance._screens.IsOpen(_instance._screen.Id);

        /// <summary>승객 화면을 열거나 닫는다(상단 바 버튼·P 키). 화면이 없으면 무시한다.</summary>
        public static void Toggle() { if (_instance != null) _instance.SetOpen(!IsOpen); }

        /// <summary>승객 화면을 연다(데모용).</summary>
        public static void Open() { if (_instance != null) _instance.SetOpen(true); }

        /// <summary>승객 화면을 닫는다.</summary>
        public static void Close() { if (_instance != null) _instance.SetOpen(false); }

        /// <summary>정원(NpcPool.json `_maxPassengers`). 풀이 아직 없으면 0.</summary>
        public static int Capacity => NpcInstaller.Pool != null ? NpcInstaller.Pool.MaxPassengers : 0;

        private void Initialize(UiScreens screens)
        {
            _screens = screens;
            _screen = GetComponent<UiScreen>();
            _passengers = GameServices.Get<IPassengers>();
            _interaction = GameServices.Get<INpcInteraction>();
            _empty.text = Strings.Get("passengers_empty");
            _hint.text = Strings.Get("passengers_drag_hint");
            TMP_Text closeLabel = _close.GetComponentInChildren<TMP_Text>(true);
            if (closeLabel != null) closeLabel.text = Strings.Get("passengers_close");
            _close.onClick.AddListener(Close);
            _passengers.Boarded += OnBoarded;
            _passengers.Disembarked += OnDisembarked;
            _passengers.Left += OnLeft;
            _passengers.Returned += OnDisembarked;
            ClearRows(); // 프리팹 미리보기 줄
            SetOpen(false);
            ScreenTicker.Add(Tick);
            if (NpcHomesDemoDriver.Stage == DEMO_STAGE) NpcHomesDemoDriver.StageReady += OnDemoStageReady;
        }

        private void OnDemoStageReady(string firstNpcId) => Open();

        /// <summary>스펙 005. npcId의 끌기를 시작한다. 시작되면 패널을 감추고 끝날 때 되돌리도록 구독한다. 끌 수 없으면 그대로 둔다.</summary>
        public void BeginRowDrag(string npcId)
        {
            NpcDragController drag = NpcDragController.Instance;
            if (drag == null || !drag.BeginPassenger(npcId)) return;
            drag.PassengerDragEnded += OnRowDragEnded;
            SetPanelVisible(false);
        }

        /// <summary>스펙 005. 승객 끌기가 끝나면 구독을 풀고 패널을 되돌린다. placed면 승객 목록을 닫는다.</summary>
        private void OnRowDragEnded(bool placed)
        {
            NpcDragController.Instance.PassengerDragEnded -= OnRowDragEnded;
            SetPanelVisible(true);
            if (placed) Close();
        }

        /// <summary>스펙 005. visible에 따라 패널의 투명도와 입력 차단을 바꾼다. 화면은 끄지 않아 끌기 이벤트가 이어진다.</summary>
        private void SetPanelVisible(bool visible)
        {
            _panelGroup.alpha = visible ? 1f : 0f;
            _panelGroup.blocksRaycasts = visible;
        }

        private void SetOpen(bool open)
        {
            if (open) _screens.Show(_screen.Id); else _screens.Hide(_screen.Id);
            if (open) Rebuild();
        }

        private void OnBoarded(string npcId, string waypointId) { if (IsOpen) Rebuild(); }
        private void OnDisembarked(string npcId, string hubId) { if (IsOpen) Rebuild(); }
        private void OnLeft(string npcId) { if (IsOpen) Rebuild(); }

        /// <summary>화면이 꺼져 있어도 도는 일: P 키로 열고 닫기, 열린 동안 Esc로 닫기.</summary>
        private void Tick()
        {
            Keyboard keyboard = Keyboard.current;
            // 끄는 중의 Esc는 끌기 취소이며 패널을 닫지 않는다.
            if (keyboard == null || NpcDragController.IsInputHeld) return;
            if (keyboard.pKey.wasPressedThisFrame) Toggle();
            else if (IsOpen && keyboard.escapeKey.wasPressedThisFrame) Close();
        }

        // ----- 그리기 -----

        private void Rebuild()
        {
            if (_interaction == null) _interaction = GameServices.Get<INpcInteraction>();
            IReadOnlyList<string> ids = _passengers.PassengerIds;
            _title.text = string.Format(Strings.Get("passengers_title"), ids.Count, Capacity);
            ClearRows();
            foreach (string id in ids)
            {
                PassengerRowView row = Instantiate(_rowPrefab, _list);
                row.name = "Row_" + id;
                string name = _interaction != null ? _interaction.DisplayName(id) : id;
                row.Show(id, PassengerRequest.RowName(NpcInstaller.Pool?.Find(id), name), InteriorNpcWalker.BodySprite(id));
            }
            _empty.gameObject.SetActive(ids.Count == 0);
            _hint.gameObject.SetActive(ids.Count > 0);
        }

        private void ClearRows()
        {
            for (int i = _list.childCount - 1; i >= 0; i--)
            {
                GameObject child = _list.GetChild(i).gameObject;
                child.SetActive(false);
                Destroy(child);
            }
        }

        void OnDestroy()
        {
            GameServices.Ready -= Create;
            if (_instance != this) return;
            ScreenTicker.Remove(Tick);
            NpcHomesDemoDriver.StageReady -= OnDemoStageReady;
            if (_passengers != null)
            {
                _passengers.Boarded -= OnBoarded;
                _passengers.Disembarked -= OnDisembarked;
                _passengers.Left -= OnLeft;
                _passengers.Returned -= OnDisembarked;
            }
            _instance = null;
        }
    }
}
