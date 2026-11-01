using UnityEngine;

using TMPro;

using IronRing.Contracts;
using IronRing.Contracts.Presentation;
using IronRing.Hud.Ui;
using IronRing.Localization;

namespace IronRing.Hud.Trade
{
    [RequireComponent(typeof(UiScreen))]
    public sealed class TradeScreen : MonoBehaviour
    {
        [Header("Screen references")]
        [SerializeField] private TMP_Text _title;
        [SerializeField] private TMP_Text _summary;
        [SerializeField] private TMP_Text _countdown;
        [SerializeField] private UnityEngine.UI.Button _depart;
        [SerializeField] private TradeRowView[] _rows;
        [SerializeField] private IconCatalog _icons;
        private ITradeSession _session;
        private IMarkets _markets;
        private IMoney _money;
        private ICargo _cargo;
        private UiScreens _screens;

        /// <summary>Removes the previous Ready subscription for a new run.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { GameServices.Ready -= Create; }

        /// <summary>Registers the screen binding callback after scene load.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot() { GameServices.Ready -= Create; GameServices.Ready += Create; }

        void Awake() { gameObject.SetActive(false); }

        /// <summary>Finds the screen and economy services and binds them when available.</summary>
        private static void Create()
        {
            UiScreens screens = UiScreens.Instance;
            TradeScreen screen = screens != null ? screens.Find<TradeScreen>() : null;
            if (screen == null || screen._session != null) return;
            screen._session = GameServices.Get<ITradeSession>();
            screen._markets = GameServices.Get<IMarkets>();
            screen._money = GameServices.Get<IMoney>();
            screen._cargo = GameServices.Get<ICargo>();
            if (screen._session == null || screen._markets == null || screen._money == null || screen._cargo == null) return;
            screen._screens = screens;
            foreach (TradeRowView row in screen._rows) row.Bind(screen._session, screen._markets, screen._money, screen._cargo, screens.Theme, screen._icons);
            screen._session.Opened += screen.Refresh;
            screen._session.Closed += screen.Refresh;
            screen._money.Changed += screen.Refresh;
            screen._cargo.Changed += screen.Refresh;
            screen._markets.Changed += screen.Refresh;
            screen._depart.onClick.AddListener(screen.Depart);
            screen.Refresh();
        }

        /// <summary>Reads session state and updates visibility, market title, totals and all rows.</summary>
        private void Refresh()
        {
            if (!_session.IsOpen) { _screens.Hide("Trade"); return; }
            _title.text = string.Format(Strings.Get("ui_trade_title"), Strings.Get(_session.StopId.ToLowerInvariant().Replace('-', '_')));
            _summary.text = string.Format(Strings.Get("ui_trade_summary"), _money.Balance, _cargo.Used, _cargo.Capacity);
            foreach (TradeRowView row in _rows) row.Refresh();
            _screens.Show("Trade");
        }

        /// <summary>Requests departure from the current session to close the trade screen.</summary>
        private void Depart() { _session.Depart(); }

        void Update()
        {
            if (_session != null) _countdown.text = string.Format(Strings.Get("ui_trade_countdown"), Mathf.CeilToInt(_session.SecondsLeft));
        }

        void OnDestroy()
        {
            if (_screens == null) return;
            _session.Opened -= Refresh; _session.Closed -= Refresh;
            _money.Changed -= Refresh; _cargo.Changed -= Refresh; _markets.Changed -= Refresh;
            _depart.onClick.RemoveListener(Depart);
        }
    }
}
