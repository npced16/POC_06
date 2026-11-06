using System.Collections.Generic;

using UnityEngine;
using UnityEngine.UI;

using TMPro;

using IronRing.Contracts;
using IronRing.Contracts.Presentation;
using IronRing.Hud.Ui;
using IronRing.Localization;

namespace IronRing.Hud
{
    /// <summary>
    /// 스펙 005. 녹슨 다리 찢어진 설계도 상점 화면. 상품 카드(건물 이름·가격·구매 버튼·품절이나 구매 불가 이유)와
    /// 잔고·구매 결과·[계속]을 그린다. 게임 상태는 IBlueprintShop과 IMoney에서 읽고, 입력은 상점에 넘긴다.
    /// </summary>
    [RequireComponent(typeof(UiScreen))]
    public sealed class ShopScreen : MonoBehaviour
    {
        private static ShopScreen _instance;

        [Header("상점 머리")]
        [SerializeField] private TMP_Text _title;
        [SerializeField] private TMP_Text _intro;
        [SerializeField] private TMP_Text _balance;
        [Header("상품 카드")]
        [SerializeField] private GameObject[] _cards = new GameObject[4];
        [SerializeField] private TMP_Text[] _names = new TMP_Text[4];
        [SerializeField] private TMP_Text[] _prices = new TMP_Text[4];
        [SerializeField] private TMP_Text[] _reasons = new TMP_Text[4];
        [SerializeField] private Button[] _buyButtons = new Button[4];
        [SerializeField] private TMP_Text _empty;
        [Header("구매 결과와 계속")]
        [SerializeField] private TMP_Text _result;
        [SerializeField] private Button _continue;

        private IBlueprintShop _shop;
        private IMoney _money;
        private UiScreens _screens;
        private string _screenId;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { GameServices.Ready -= Create; _instance = null; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot() { GameServices.Ready -= Create; GameServices.Ready += Create; }

        /// <summary>Ready 이후 IBlueprintShop과 캔버스의 Shop 화면을 찾아 한 번 연결한다.</summary>
        private static void Create()
        {
            if (_instance != null || GameServices.Get<IBlueprintShop>() == null) return;
            UiScreens screens = UiScreens.Instance;
            ShopScreen placed = screens != null ? screens.Find<ShopScreen>() : null;
            if (placed == null || placed._title == null || placed._continue == null) return;
            _instance = placed;
            placed.Initialize(screens);
        }

        /// <summary>screens와 상점·돈 서비스를 저장하고 구매·계속 버튼과 상점 이벤트를 연결한다.</summary>
        private void Initialize(UiScreens screens)
        {
            _screens = screens;
            _screenId = GetComponent<UiScreen>().Id;
            _shop = GameServices.Get<IBlueprintShop>();
            _money = GameServices.Get<IMoney>();
            for (int i = 0; i < _buyButtons.Length; i++)
            {
                if (_buyButtons[i] == null) continue;
                int index = i;
                _buyButtons[i].onClick.AddListener(() => Buy(index));
            }
            _continue.onClick.AddListener(_shop.Continue);
            _shop.Opened += OnOpened;
            _shop.Changed += OnChanged;
            _shop.Closed += OnClosed;
            _shop.Purchased += OnPurchased;
            if (_money != null) _money.Changed += OnChanged;
            if (_shop.IsOpen) OnOpened();
            else _screens.Hide(_screenId);
        }

        /// <summary>index번째 진열 상품 구매를 상점에 요청한다.</summary>
        private void Buy(int index)
        {
            if (index < _shop.Offers.Count) _shop.TryBuy(_shop.Offers[index]);
        }

        /// <summary>상점이 열리면 지난 구매 결과를 지우고 화면을 연 뒤 그린다.</summary>
        private void OnOpened()
        {
            _result.text = string.Empty;
            _screens.Show(_screenId);
            Refresh();
        }

        /// <summary>상점이나 잔고가 바뀌면 열려 있을 때만 다시 그린다.</summary>
        private void OnChanged()
        {
            if (_shop.IsOpen) Refresh();
        }

        /// <summary>구매한 건물 이름과 공개된 재료·수량을 결과 줄에 적는다.</summary>
        private void OnPurchased(string recipeId, string tileId)
        {
            string item = string.Format(Text("codex_fragment_item"), Text("item_" + tileId), _shop.RequiredCountOf(recipeId, tileId));
            _result.text = string.Format(Text("shop_result"), Text("habitat_type_" + recipeId), item);
        }

        /// <summary>상점이 닫히면 화면을 숨겨 뒤 화면 입력을 다시 허용한다.</summary>
        private void OnClosed() { _screens.Hide(_screenId); }

        /// <summary>제목·잔고와 진열 상품마다 이름·가격·구매 가능 여부를 그리고, 진열이 없으면 빈 상점 안내를 보인다.</summary>
        private void Refresh()
        {
            _title.text = Text("shop_title");
            _intro.text = Text("shop_intro");
            _balance.text = string.Format(Text("shop_balance"), _money != null ? _money.Balance : 0);
            IReadOnlyList<string> offers = _shop.Offers;
            for (int i = 0; i < _cards.Length; i++)
            {
                bool visible = i < offers.Count;
                _cards[i].SetActive(visible);
                if (!visible) continue;
                string id = offers[i];
                string reason = _shop.UnavailableReason(id);
                _names[i].text = Text("habitat_type_" + id);
                _prices[i].text = string.Format(Text("shop_price"), _shop.PriceOf(id));
                _reasons[i].text = reason;
                _reasons[i].gameObject.SetActive(!string.IsNullOrEmpty(reason));
                _buyButtons[i].interactable = string.IsNullOrEmpty(reason);
                TMP_Text label = _buyButtons[i].GetComponentInChildren<TMP_Text>(true);
                if (label != null) label.text = _shop.IsSoldOut(id) ? Text("shop_reason_sold_out") : Text("shop_buy");
            }
            _empty.text = Text("shop_empty");
            _empty.gameObject.SetActive(offers.Count == 0);
            TMP_Text continueLabel = _continue.GetComponentInChildren<TMP_Text>(true);
            if (continueLabel != null) continueLabel.text = Text("loop_continue");
        }

        /// <summary>ko.json에서 key 문구를 찾아 반환하고 없으면 key를 그대로 반환한다.</summary>
        private static string Text(string key)
        {
            try { return Strings.Get(key); }
            catch (KeyNotFoundException) { return key; }
        }

        void OnDestroy()
        {
            GameServices.Ready -= Create;
            if (_shop != null)
            {
                _shop.Opened -= OnOpened;
                _shop.Changed -= OnChanged;
                _shop.Closed -= OnClosed;
                _shop.Purchased -= OnPurchased;
            }
            if (_money != null) _money.Changed -= OnChanged;
            if (_instance == this) _instance = null;
        }
    }
}
