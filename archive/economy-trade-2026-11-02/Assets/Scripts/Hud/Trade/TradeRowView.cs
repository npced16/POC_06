using UnityEngine;

using TMPro;

using IronRing.Contracts;
using IronRing.Economy;
using IronRing.Hud.Ui;
using IronRing.Localization;

namespace IronRing.Hud.Trade
{
    public sealed class TradeRowView : MonoBehaviour
    {
        [Header("Screen references")]
        [SerializeField] private string _goodsId;
        [SerializeField] private UnityEngine.UI.Image _icon;
        [SerializeField] private TMP_Text _name;
        [SerializeField] private TMP_Text _price;
        [SerializeField] private TMP_Text _stock;
        [SerializeField] private TMP_Text _cargoCount;
        [SerializeField] private UnityEngine.UI.Button _buy;
        [SerializeField] private UnityEngine.UI.Button _sell;
        private ITradeSession _session;
        private IMarkets _markets;
        private IMoney _money;
        private ICargo _cargo;
        private UiTheme _theme;

        /// <summary>Stores the supplied services, theme and icons and binds the row buttons.</summary>
        public void Bind(ITradeSession session, IMarkets markets, IMoney money, ICargo cargo, UiTheme theme, IconCatalog icons)
        {
            _session = session; _markets = markets; _money = money; _cargo = cargo; _theme = theme;
            _name.text = EconomyInstaller.Goods.NameOf(_goodsId);
            _icon.sprite = icons.Get(EconomyInstaller.Goods.Get(_goodsId).IconKey);
            _buy.onClick.AddListener(Buy);
            _sell.onClick.AddListener(Sell);
        }

        /// <summary>Reads the current market and cargo values and updates row labels and button availability.</summary>
        public void Refresh()
        {
            string stop = _session.StopId;
            _price.text = string.Format(Strings.Get("ui_trade_price"), _markets.Price(stop, _goodsId), _markets.SellPrice(stop, _goodsId));
            _stock.text = string.Format(Strings.Get("ui_trade_stock"), _markets.Stock(stop, _goodsId), _markets.Demand(stop, _goodsId));
            _cargoCount.text = string.Format(Strings.Get("ui_trade_owned"), _cargo.CountOf(_goodsId));
            int cost = _markets.BuyCost(stop, _goodsId, 1);
            SetButton(_buy, _session.IsOpen && cost >= 0 && _money.Balance >= cost && _cargo.Used < _cargo.Capacity && _markets.Stock(stop, _goodsId) > 0);
            SetButton(_sell, _session.IsOpen && _cargo.CountOf(_goodsId) > 0 && _markets.Demand(stop, _goodsId) > 0);
        }

        /// <summary>Sets button interaction and label color from on and the stored theme.</summary>
        private void SetButton(UnityEngine.UI.Button button, bool on)
        {
            button.interactable = on;
            button.GetComponentInChildren<TMP_Text>().color = on ? _theme.Text : _theme.Muted;
        }

        /// <summary>Requests one item purchase from the open session market and refreshes the row.</summary>
        private void Buy() { if (_session.IsOpen) _markets.TryBuy(_session.StopId, _goodsId, 1); Refresh(); }

        /// <summary>Requests one item sale from the open session market and refreshes the row.</summary>
        private void Sell() { if (_session.IsOpen) _markets.TrySell(_session.StopId, _goodsId, 1); Refresh(); }

        void OnDestroy() { _buy.onClick.RemoveListener(Buy); _sell.onClick.RemoveListener(Sell); }
    }
}
