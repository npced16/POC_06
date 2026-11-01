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
    /// 스토리 040·042·045(가벼운 경제) 조립기.
    /// Composing: Resources/Economy/Economy.json으로 돈·장부·정산·배치 비용을 만들고 IMoney·ILedger·IPlacementCost로 등록한다.
    /// Ready: 승객 운임을 연결하고, 서식지(같은 Composing의 HabitatsInstaller·ItemsInstaller)가 생긴 뒤 TownTiers.json으로
    /// 주민(IPopulation)과 마을 등급(ITownTier)을 만들어 정산에 건다:
    ///   Settlement.Income = 완성 건물 _income 합, AfterSettle = 마을 등급 판정(주민·번영) → 주민 증감 → RunState.RefreshPhase.
    /// RunState에 TownLink(등급 → HubPhase 사상, 등급, 주민)를 붙이고, 레시피 도감에 등급 해금을 연결한다.
    /// ITrackRuntime이 없는 씬(다른 스토리의 독립 데모)에서는 아무것도 하지 않는다. 정적 상태는 SubsystemRegistration에서 지운다.
    /// </summary>
    public static class EconomyInstaller
    {
        /// <summary>경제 수치 리소스 경로.</summary>
        public const string SettingsPath = "Economy/Economy";
        /// <summary>마을 등급 리소스 경로.</summary>
        public const string TownPath = "Economy/TownTiers";

        /// <summary>설치된 수치(설치 전 null).</summary>
        public static EconomySettings Settings { get; private set; }
        /// <summary>설치된 돈.</summary>
        public static MoneyStore Money { get; private set; }
        /// <summary>설치된 장부.</summary>
        public static Ledger Ledger { get; private set; }
        /// <summary>설치된 정산 규칙.</summary>
        public static Settlement Settlement { get; private set; }
        /// <summary>스토리 041. 설치된 배치 비용.</summary>
        public static PlacementCost Cost { get; private set; }
        /// <summary>스토리 042. 마을 수치.</summary>
        public static TownSettings Town { get; private set; }
        /// <summary>스토리 042. 주민.</summary>
        public static Population Population { get; private set; }
        /// <summary>스토리 042. 마을 등급.</summary>
        public static TownTiers Tiers { get; private set; }

        private static EconomyDemoDriver _demo;
        private static TownDemoDriver _townDemo;
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
            _demo = null;
            _townDemo = null;
            if (_paidPlacement != null) { _paidPlacement.TryCharge = null; _paidPlacement.CanAfford = null; _paidPlacement.Refund = null; _paidPlacement = null; }
            Tiers?.Clear();
            Population?.Dispose();
            Settlement?.Dispose();
            Tiers = null; Population = null; Town = null; Cost = null;
            Settlement = null; Ledger = null; Money = null; Settings = null;
        }

        /// <summary>돈·장부·정산·배치 비용을 만들고 등록한다. 선로가 없으면 건너뛴다.</summary>
        private static void Install()
        {
            ITrackRuntime track = GameServices.Get<ITrackRuntime>();
            if (track == null) return;
            Teardown();
            Settings = EconomySettings.Parse(Load(SettingsPath));
            Money = new MoneyStore(Settings.StartBalance);
            Ledger = new Ledger(track.Lap);
            RunState run = GameServices.Get<IHubStateSystem>() as RunState;
            Func<bool> over = () => run != null && run.IsRunLost;
            Settlement = new Settlement(Settings, Money, Ledger, track, Strings.Get("econ_lap_settled"), Strings.Get("econ_bankrupt"),
                reason => run?.DeclareBankrupt(reason), over);
            Settlement.Settled += ShowSettleToast;
            Cost = new PlacementCost(id => IronRing.Items.ItemsInstaller.Catalog?.Get(id)?.Price ?? 0, Money, Settings.RefundRatio);
            GameServices.Register<IMoney>(Money);
            GameServices.Register<ILedger>(Ledger);
            GameServices.Register<IPlacementCost>(Cost);
            string stage = EconomyDemoDriver.ReadStage();
            if (stage != null) _demo = new EconomyDemoDriver(stage, Settlement);
            ScreenTicker.Add(Tick);
            Debug.Log($"ECON: installed balance={Money.Balance} upkeep={Settings.TrainUpkeep} fare={Settings.Fare} bankruptLaps={Settings.BankruptLaps} demo={stage ?? "none"}");
        }

        /// <summary>승객·서식지·배치는 같은 Composing의 다른 설치자가 등록하므로 Ready에서 연결한다.</summary>
        private static void OnReady()
        {
            if (Settlement == null) return;
            Settlement.BindPassengers(GameServices.Get<IPassengers>());
            BindPlacement();
            BindTown();
        }

        /// <summary>스토리 041. ItemPlacement에 결제·환급을 건다.</summary>
        private static void BindPlacement()
        {
            if (_paidPlacement != null) return;
            _paidPlacement = GameServices.Get<IItemPlacement>() as IronRing.Items.ItemPlacement;
            if (_paidPlacement == null || Cost == null) return;
            _paidPlacement.TryCharge = Cost.TryCharge;
            _paidPlacement.CanAfford = Cost.CanAfford;
            _paidPlacement.Refund = Cost.Refund;
        }

        /// <summary>스토리 042. 주민·마을 등급을 만들고 정산·RunState·도감에 건다.</summary>
        private static void BindTown()
        {
            if (Population != null) return;
            IHabitatClusters clusters = GameServices.Get<IHabitatClusters>();
            IronRing.Habitats.HabitatCatalog catalog = IronRing.Habitats.HabitatsInstaller.Catalog;
            if (clusters == null || catalog == null) { Debug.LogWarning("TOWN: 서식지가 없어 주민·등급을 건너뜁니다."); return; }
            Town = TownSettings.Parse(Load(TownPath));
            Population = new Population(Town, catalog, clusters);
            Tiers = new TownTiers(Town);
            RunState run = GameServices.Get<IHubStateSystem>() as RunState;
            string hub = Town.HubId;
            Population pop = Population;
            TownTiers tiers = Tiers;
            Settlement.Income = () => pop.Income;
            Settlement.AfterSettle = lap =>
            {
                float prosperity = run != null ? run.GetProsperity(hub) : 0f;
                tiers.Evaluate(pop.Residents, prosperity);
                pop.Settle(lap);
                run?.RefreshPhase(hub);
                Debug.Log($"TOWN: lap={lap} tier={tiers.Tier} {tiers.TierName} residents={pop.Residents} prosperity={prosperity:0.#} next={(tiers.IsMax ? "-" : tiers.NameOf(tiers.NextTier))} need residents {tiers.NextResidentsNeed} prosperity {tiers.NextProsperityNeed} phase={tiers.Phase}");
            };
            Tiers.TierChanged += OnTierChanged;
            Tiers.Collapsing += OnCollapsing;
            run?.AttachTown(new TownLink
            {
                HubId = hub, Phase = () => tiers.Phase, Tier = () => tiers.Tier, Residents = () => pop.Residents,
                VictoryTier = Town.VictoryTier, PartialTier = Town.PartialTier,
            });
            IronRing.Habitats.HabitatsInstaller.Codex?.BindTiers(Tiers);
            GameServices.Register<IPopulation>(Population);
            GameServices.Register<ITownTier>(Tiers);
            string stage = TownDemoDriver.ReadStage();
            if (stage != null) _townDemo = new TownDemoDriver(stage, Town, Settlement, Population, Tiers, run);
            Debug.Log($"TOWN: installed hub={hub} residents={Population.Residents} housing={Population.Housing} light={Population.Light} income={Population.Income} tier={Tiers.TierName} townTiers={run?.TownActive ?? false} demo={stage ?? "none"}");
        }

        private static void OnTierChanged(int tier)
        {
            string name = Tiers.NameOf(tier);
            TownTierData data = Tiers.Data(tier);
            Debug.Log($"TOWN: tier up {name} ({data.Id}) recipes=[{string.Join(",", data.UnlockRecipes)}] upgrades=[{string.Join(",", data.UnlockUpgrades)}] stops=[{string.Join(",", data.UnlockStops)}]");
            ToastScreen.Show(string.Format(Strings.Get("town_tier_up"), Town.HubName, name), Settings != null ? Settings.SettleToastSeconds : 4f);
        }

        private static void OnCollapsing()
        {
            Debug.Log($"TOWN: collapsed {Town.HubId}");
            ToastScreen.Show(string.Format(Strings.Get("town_collapsed"), Town.HubName), Settings != null ? Settings.SettleToastSeconds : 4f);
        }

        private static void Tick()
        {
            float dt = Time.deltaTime;
            _demo?.Tick(dt);
            _townDemo?.Tick(dt);
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
