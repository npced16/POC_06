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

namespace IronRing.Hud.Npc
{
    /// <summary>
    /// 스토리 025. 상황 화면 "Passengers": 지금 열차에 탄 승객마다 한 줄(부품 PassengerRow) — 직업명·출신지·탄 지 몇 바퀴·제안·힌트.
    /// 비어 있으면 "승객이 없다". 상단 바 승객 버튼(Toggle)·P 키로 열고 닫고, 닫기 버튼·Esc로 닫는다. 탑승·하차·이탈 때 다시 그린다.
    /// 숨은 조건은 보이지 않는다(IPassengers.Hint 문장만). 게임 상태는 읽기만 한다. 키는 화면이 꺼져 있어도 ScreenTicker로 읽는다.
    /// </summary>
    [RequireComponent(typeof(UiScreen))]
    public sealed class PassengersScreen : MonoBehaviour
    {
        private const string DEMO_STAGE = "passengers";
        private static PassengersScreen _instance;

        /// <summary>현재 승객 화면. 초기화 전이면 null.</summary>
        public static PassengersScreen Instance => _instance;

        [SerializeField] private TMP_Text _title;
        [SerializeField] private TMP_Text _hint;
        [SerializeField] private TMP_Text _empty;
        [SerializeField] private Button _close;
        [SerializeField] private RectTransform _list;
        [Tooltip("승객 한 줄 부품(Parts/PassengerRow.prefab).")]
        [SerializeField] private PassengerRowView _rowPrefab;

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
            _hint.text = Strings.Get("passengers_hint");
            _empty.text = Strings.Get("passengers_empty");
            TMP_Text closeLabel = _close.GetComponentInChildren<TMP_Text>(true);
            if (closeLabel != null) closeLabel.text = Strings.Get("passengers_close");
            _close.onClick.AddListener(Close);
            _passengers.Boarded += OnBoarded;
            _passengers.Disembarked += OnDisembarked;
            _passengers.Left += OnLeft;
            ClearRows(); // 프리팹 미리보기 줄
            SetOpen(false);
            ScreenTicker.Add(Tick);
            if (NpcHomesDemoDriver.Stage == DEMO_STAGE) NpcHomesDemoDriver.StageReady += OnDemoStageReady;
        }

        private void OnDemoStageReady(string firstNpcId) => Open();

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
            if (keyboard == null) return;
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
                string origin = _passengers.OriginName(id);
                row.Show(_interaction != null ? _interaction.DisplayName(id) : id,
                    string.IsNullOrEmpty(origin) ? null : string.Format(Strings.Get("passengers_origin"), origin),
                    string.Format(Strings.Get("passengers_laps"), _passengers.LapsAboard(id)),
                    Quote(_passengers.Proposal(id)),
                    _passengers.Hint(id));
            }
            _empty.gameObject.SetActive(ids.Count == 0);
        }

        private static string Quote(string text) => string.IsNullOrEmpty(text) ? null : "“" + text + "”";

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
            }
            _instance = null;
        }
    }
}
