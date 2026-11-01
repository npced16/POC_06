using UnityEngine;
using UnityEngine.InputSystem;

using TMPro;

using IronRing.Contracts;
using IronRing.Contracts.Presentation;
using IronRing.Hud.Hotbar;
using IronRing.Hud.Ui;
using IronRing.Localization;

namespace IronRing.Hud.Warehouse
{
    [RequireComponent(typeof(UiScreen))]
    public sealed class WarehouseScreen : MonoBehaviour
    {
        private static WarehouseScreen _instance;

        [Header("창고 화면 참조")]
        [SerializeField] private TMP_Text _title;
        [SerializeField] private TMP_Text _cargoTotal;
        [SerializeField] private UnityEngine.UI.Button _close;
        [SerializeField] private WarehouseRowView[] _rows;
        [SerializeField] private IconCatalog _icons;
        private IWarehouse _warehouse;
        private ICargo _cargo;
        private ITransfer _transfer;
        private IViewMode _view;
        private ITrackRuntime _track;
        private bool _wasHeld;
        private UiScreens _screens;

        public static bool IsOpen => _instance != null && _instance._screens.IsOpen("Warehouse");
        public static bool CanOpen => _instance != null && _instance._view.InteriorOpen && _instance._transfer.CanTransfer;

        /// <summary>새 실행에서 이전 서비스 준비 구독과 창고 화면 인스턴스를 초기화한다.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { GameServices.Ready -= Create; _instance = null; }

        /// <summary>씬 로드 뒤 서비스 준비 시 창고를 연결하도록 구독한다.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot() { GameServices.Ready -= Create; GameServices.Ready += Create; }

        /// <summary>창고 화면과 필요한 서비스를 찾아 모두 준비되었을 때만 참조와 갱신 이벤트를 연결한다.</summary>
        private static void Create()
        {
            if (_instance != null) return;
            UiScreens screens = UiScreens.Instance;
            WarehouseScreen screen = screens != null ? screens.Find<WarehouseScreen>() : null;
            if (screen == null) return;
            screen._warehouse = GameServices.Get<IWarehouse>(); screen._cargo = GameServices.Get<ICargo>();
            screen._transfer = GameServices.Get<ITransfer>(); screen._view = GameServices.Get<IViewMode>();
            screen._track = GameServices.Get<ITrackRuntime>();
            if (screen._warehouse == null || screen._cargo == null || screen._transfer == null || screen._view == null || screen._track == null) return;
            screen._screens = screens; screen._wasHeld = screen._track.IsHeld; _instance = screen;
            foreach (WarehouseRowView row in screen._rows) row.Bind(screen._warehouse, screen._cargo, screen._transfer, screens.Theme, screen._icons);
            screen._close.onClick.AddListener(Close);
            screen._warehouse.Changed += screen.Refresh; screen._cargo.Changed += screen.Refresh;
            screen._view.InteriorChanged += screen.OnInteriorChanged;
            ScreenTicker.Add(screen.Tick);
            Close();
        }

        /// <summary>창고를 열거나 닫는다. 열 때 이송 가능 여부를 검사하고 팔레트 및 배치 선택을 정리한다.</summary>
        public static void Toggle()
        {
            if (IsOpen) { Close(); return; }
            if (!CanOpen) return;
            HotbarScreen.Instance?.PrepareForWarehouse();
            _instance.Refresh(); _instance._screens.Show("Warehouse");
        }

        /// <summary>등록된 창고 화면을 숨긴다. 준비되지 않은 화면은 무시한다.</summary>
        public static void Close() { if (_instance != null) _instance._screens.Hide("Warehouse"); }

        /// <summary>현재 거점 이름과 창고·화물 총량 및 품목 여섯 행을 서비스 값으로 갱신한다.</summary>
        private void Refresh()
        {
            if (!CanOpen) { Close(); return; }
            string hubKey = _view.InteriorHubId.ToLowerInvariant().Replace('-', '_');
            _title.text = string.Format(Strings.Get("ui_warehouse_title"), Strings.Get(hubKey), _warehouse.Total, _warehouse.Capacity);
            _cargoTotal.text = string.Format(Strings.Get("ui_warehouse_cargo"), _cargo.Used, _cargo.Capacity);
            foreach (WarehouseRowView row in _rows) row.Refresh();
        }

        /// <summary>내부 화면 open 상태가 바뀌면 닫거나, 열린 창고의 거점과 수량을 갱신한다.</summary>
        private void OnInteriorChanged(bool open) { if (!open) Close(); else if (IsOpen) Refresh(); }

        /// <summary>창고가 꺼져 있어도 W 키를 읽으며 출발·내부 이탈 시 자동으로 닫는다.</summary>
        private void Tick()
        {
            if (IsOpen && (!CanOpen || (_wasHeld && !_track.IsHeld))) Close();
            _wasHeld = _track.IsHeld;
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (keyboard.wKey.wasPressedThisFrame) Toggle();
            else if (IsOpen && keyboard.escapeKey.wasPressedThisFrame) Close();
        }

        void OnDestroy()
        {
            if (_screens == null) return;
            ScreenTicker.Remove(Tick);
            _warehouse.Changed -= Refresh; _cargo.Changed -= Refresh; _view.InteriorChanged -= OnInteriorChanged;
            _close.onClick.RemoveListener(Close);
            if (_instance == this) _instance = null;
        }
    }
}
