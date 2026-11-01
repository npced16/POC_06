using System;

using UnityEngine;

using IronRing.Contracts;
using IronRing.Contracts.Presentation;
using IronRing.Economy.Demo;
using IronRing.Hub;
using IronRing.Hud.Ui;
using IronRing.Localization;

namespace IronRing.Economy
{
    /// <summary>
    /// 스토리 040 조립기. GameServices.Composing에서 Resources/Economy/{Economy,Goods,Markets}.json을 읽어 경제를 만들고
    /// IMoney·ICargo·IMarkets·ILedger·ITradeSession으로 등록한다. ITrackRuntime이 없는 씬(다른 스토리의 독립 데모)에서는 아무것도 하지 않는다.
    /// Ready에서 IPassengers(운임)를 연결하고, 매 프레임 일(거래 세션 자동 출발, 데모)은 ScreenTicker로 돌린다.
    /// 정산 토스트(로직 문구 econ_lap_settled)는 ToastScreen으로 띄운다. 파산은 RunState.DeclareBankrupt(econ_bankrupt).
    /// 도메인 재로드가 꺼져 있으므로 정적 상태는 SubsystemRegistration에서 지운다(RunResultScreen.Restart도 이 훅을 다시 부른다).
    /// </summary>
    public static class EconomyInstaller
    {
        /// <summary>경제 수치 리소스 경로.</summary>
        public const string SettingsPath = "Economy/Economy";
        /// <summary>품목 리소스 경로.</summary>
        public const string GoodsPath = "Economy/Goods";
        /// <summary>시장 리소스 경로.</summary>
        public const string MarketsPath = "Economy/Markets";

        /// <summary>설치된 수치(설치 전 null).</summary>
        public static EconomySettings Settings { get; private set; }
        /// <summary>설치된 품목 카탈로그(이름·아이콘 키·색; 설치 전 null).</summary>
        public static GoodsCatalog Goods { get; private set; }
        /// <summary>설치된 돈.</summary>
        public static MoneyStore Money { get; private set; }
        /// <summary>설치된 화물칸.</summary>
        public static Cargo Cargo { get; private set; }
        /// <summary>설치된 시장.</summary>
        public static Markets Markets { get; private set; }
        /// <summary>설치된 장부.</summary>
        public static Ledger Ledger { get; private set; }
        /// <summary>설치된 정산·파산 규칙.</summary>
        public static Settlement Settlement { get; private set; }
        /// <summary>설치된 거래 세션.</summary>
        public static TradeSession Session { get; private set; }

        /// <summary>스토리 041. 설치된 거점 창고.</summary>
        public static Warehouse Warehouse { get; private set; }
        /// <summary>스토리 041. 설치된 창고 ↔ 화물칸 이동.</summary>
        public static Transfer Transfer { get; private set; }
        /// <summary>스토리 041. 설치된 배치 비용.</summary>
        public static PlacementCost Cost { get; private set; }
        /// <summary>스토리 041. 설치된 생산(Ready에서 만든다).</summary>
        public static Production Production { get; private set; }

        private static EconomyDemoDriver _demo;
        private static ProductionDemoDriver _productionDemo;
        private static IronRing.Items.ItemPlacement _paidPlacement;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            GameServices.Composing -= Install;
            GameServices.Ready -= OnReady;
            Teardown();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Subscribe()
        {
            GameServices.Composing -= Install;
            GameServices.Composing += Install;
            GameServices.Ready -= OnReady;
            GameServices.Ready += OnReady;
        }

        private static void Teardown()
        {
            ScreenTicker.Remove(Tick);
            _demo?.Dispose();
            _demo = null;
            _productionDemo?.Dispose();
            _productionDemo = null;
            if (_paidPlacement != null) { _paidPlacement.TryCharge = null; _paidPlacement.CanAfford = null; _paidPlacement.Refund = null; _paidPlacement = null; }
            Production?.Dispose();
            Production = null; Warehouse = null; Transfer = null; Cost = null;
            Settlement?.Dispose();
            Session?.Dispose();
            Settlement = null; Session = null; Markets = null; Ledger = null; Cargo = null; Money = null; Goods = null; Settings = null;
        }

        /// <summary>경제를 만들고 등록한다. 선로가 없으면 건너뛴다.</summary>
        private static void Install()
        {
            ITrackRuntime track = GameServices.Get<ITrackRuntime>();
            if (track == null) return;
            Teardown();
            Settings = EconomySettings.Parse(Load(SettingsPath));
            string stage = EconomyDemoDriver.ReadStage();
            EconomyDemoDriver.ApplyOverrides(stage, Settings);
            Goods = GoodsCatalog.Parse(Load(GoodsPath));
            Money = new MoneyStore(Settings.StartBalance);
            Cargo = new Cargo(Goods, Settings.CargoCapacity);
            Ledger = new Ledger(track.Lap);
            Markets = new Markets(MarketsData.Parse(Load(MarketsPath)), Goods, Settings, Money, Cargo, Ledger);
            RunState run = GameServices.Get<IHubStateSystem>() as RunState;
            Func<bool> over = () => run != null && run.IsRunLost;
            Settlement = new Settlement(Settings, Money, Ledger, Markets, track, Strings.Get("econ_lap_settled"), Strings.Get("econ_bankrupt"),
                reason => run?.DeclareBankrupt(reason), over);
            Settlement.Settled += ShowSettleToast;
            Session = new TradeSession(track, Markets, Settings.AutoDepartSeconds, over);
            GameServices.Register<IMoney>(Money);
            GameServices.Register<ICargo>(Cargo);
            GameServices.Register<IMarkets>(Markets);
            GameServices.Register<ILedger>(Ledger);
            GameServices.Register<ITradeSession>(Session);
            GoodsCatalog goods = Goods;
            Warehouse = new Warehouse(id => goods.Get(id) != null, Settings.WarehouseCapacity);
            string hub = Settings.WarehouseHubId;
            Transfer = new Transfer(Warehouse, Cargo, () =>
            {
                IViewMode view = GameServices.Get<IViewMode>();
                return view != null && view.InteriorOpen && view.InteriorHubId == hub;
            });
            Cost = new PlacementCost(id => IronRing.Items.ItemsInstaller.Catalog?.Get(id)?.Price ?? 0, Money, Settings.RefundRatio);
            GameServices.Register<IWarehouse>(Warehouse);
            GameServices.Register<ITransfer>(Transfer);
            GameServices.Register<IPlacementCost>(Cost);
            if (stage != null) _demo = new EconomyDemoDriver(stage, Settings, track, Markets, Cargo, Money, Ledger, Session, run);
            ScreenTicker.Add(Tick);
            Debug.Log($"ECON: installed balance={Money.Balance} upkeep={Settings.TrainUpkeep} fare={Settings.Fare} cargo={Cargo.Capacity} stops={Markets.StopIds.Count} goods={Goods.Ids.Count} demo={stage ?? "none"}");
        }

        /// <summary>승객 서비스는 같은 Composing의 NpcInstaller가 등록하므로 Ready에서 연결한다.</summary>
        private static void OnReady()
        {
            if (Settlement == null) return;
            Settlement.BindPassengers(GameServices.Get<IPassengers>());
            BindProduction();
        }

        /// <summary>
        /// 스토리 041. 서식지·배치는 같은 Composing의 ItemsInstaller가 등록하므로 Ready에서 연결한다:
        /// ItemPlacement에 결제·환급을 걸고, Production을 정산(Settlement.Producing)에 걸어 IProductionStatus로 등록한다.
        /// </summary>
        private static void BindProduction()
        {
            if (Production != null) return;
            _paidPlacement = GameServices.Get<IItemPlacement>() as IronRing.Items.ItemPlacement;
            if (_paidPlacement != null && Cost != null)
            {
                _paidPlacement.TryCharge = Cost.TryCharge;
                _paidPlacement.CanAfford = Cost.CanAfford;
                _paidPlacement.Refund = Cost.Refund;
            }
            IHabitatClusters clusters = GameServices.Get<IHabitatClusters>();
            IronRing.Habitats.HabitatCatalog catalog = IronRing.Habitats.HabitatsInstaller.Catalog;
            if (clusters == null || catalog == null) { Debug.LogWarning("ECON: 서식지가 없어 생산을 건너뜁니다."); return; }
            Production = new Production(Settings.WarehouseHubId, catalog, clusters, Warehouse, Money, Ledger, GameServices.Get<IItemPlacement>());
            Settlement.Producing = Production.Tick;
            GameServices.Register<IProductionStatus>(Production);
            string stage = ProductionDemoDriver.ReadStage();
            if (stage != null)
                _productionDemo = new ProductionDemoDriver(stage, Settings, GameServices.Get<IItemPlacement>(), GameServices.Get<IHubInterior>(),
                    GameServices.Get<IInteriorAnchors>(), GameServices.Get<IInteriorNavigation>(), Warehouse, Transfer, Cargo, Money, Ledger, Settlement, Production);
            Debug.Log($"PROD: installed hub={Settings.WarehouseHubId} warehouse={Warehouse.Capacity} refund={Settings.RefundRatio} placementPaid={_paidPlacement != null} demo={stage ?? "none"}");
        }

        private static void Tick()
        {
            float dt = Time.deltaTime;
            Session?.Tick(dt);
            _demo?.Tick(dt);
            _productionDemo?.Tick(dt);
        }

        private static void ShowSettleToast(string line) => ToastScreen.Show(line, Settings != null ? Settings.SettleToastSeconds : 4f);

        private static string Load(string path)
        {
            TextAsset asset = Resources.Load<TextAsset>(path);
            if (asset == null) throw new InvalidOperationException("Missing economy resource: Resources/" + path + ".json");
            return asset.text;
        }
    }
}
