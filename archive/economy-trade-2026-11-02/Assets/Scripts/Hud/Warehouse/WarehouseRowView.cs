using UnityEngine;

using TMPro;

using IronRing.Contracts;
using IronRing.Economy;
using IronRing.Hud.Ui;
using IronRing.Localization;

namespace IronRing.Hud.Warehouse
{
    public sealed class WarehouseRowView : MonoBehaviour
    {
        [Header("품목과 화면 참조")]
        [SerializeField] private string _goodsId;
        [SerializeField] private UnityEngine.UI.Image _icon;
        [SerializeField] private TMP_Text _name;
        [SerializeField] private TMP_Text _stock;
        [SerializeField] private TMP_Text _cargoCount;
        [SerializeField] private UnityEngine.UI.Button _load;
        [SerializeField] private UnityEngine.UI.Button _unload;
        private IWarehouse _warehouse;
        private ICargo _cargo;
        private ITransfer _transfer;
        private UiTheme _theme;

        /// <summary>주어진 창고·화물·이송 서비스와 테마·아이콘을 저장하고 한 개 이동 버튼을 연결한다.</summary>
        public void Bind(IWarehouse warehouse, ICargo cargo, ITransfer transfer, UiTheme theme, IconCatalog icons)
        {
            _warehouse = warehouse; _cargo = cargo; _transfer = transfer; _theme = theme;
            _name.text = EconomyInstaller.Goods.NameOf(_goodsId);
            _icon.sprite = icons.Get(EconomyInstaller.Goods.Get(_goodsId).IconKey);
            _load.onClick.AddListener(Load);
            _unload.onClick.AddListener(Unload);
        }

        /// <summary>저장된 서비스의 품목 수량과 여유 공간을 읽어 라벨 및 이동 버튼을 갱신한다.</summary>
        public void Refresh()
        {
            _stock.text = string.Format(Strings.Get("ui_warehouse_stock"), _warehouse.CountOf(_goodsId));
            _cargoCount.text = string.Format(Strings.Get("ui_warehouse_train"), _cargo.CountOf(_goodsId));
            SetButton(_load, _transfer.CanTransfer && _warehouse.CountOf(_goodsId) > 0 && _cargo.Used < _cargo.Capacity);
            SetButton(_unload, _transfer.CanTransfer && _cargo.CountOf(_goodsId) > 0 && _warehouse.Total < _warehouse.Capacity);
        }

        /// <summary>button의 입력 가능 여부와 글자색을 on 및 테마에 맞게 변경한다.</summary>
        private void SetButton(UnityEngine.UI.Button button, bool on)
        {
            button.interactable = on;
            button.GetComponentInChildren<TMP_Text>().color = on ? _theme.Text : _theme.Muted;
        }

        /// <summary>이송 서비스에 현재 품목 한 개를 열차에 싣도록 요청하고 표시를 갱신한다.</summary>
        private void Load() { _transfer.TryLoad(_goodsId, 1); Refresh(); }

        /// <summary>이송 서비스에 현재 품목 한 개를 창고로 내리도록 요청하고 표시를 갱신한다.</summary>
        private void Unload() { _transfer.TryUnload(_goodsId, 1); Refresh(); }

        void OnDestroy() { _load.onClick.RemoveListener(Load); _unload.onClick.RemoveListener(Unload); }
    }
}
