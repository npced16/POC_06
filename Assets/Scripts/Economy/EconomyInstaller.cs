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
    /// 스토리 048: Composing에서 ILineOpener(LineOpener, 잠긴 역)를 등록하고 RunState에 잠긴 역을 닫아 둔다. 주민·등급은 열린 마을마다:
    ///   AfterSettle = 열린 마을마다 등급 판정 → 주민 증감 → RefreshPhase. 황동골 증기역 완성 → ILineOpener.Open → 다음 정산부터 잿골도 센다.
    ///   유지비 0이면 토스트·장부에서 빠진다(Settlement.PartFormats).
    /// ITrackRuntime이 없는 씬(다른 스토리의 독립 데모)에서는 아무것도 하지 않는다. 정적 상태는 SubsystemRegistration에서 지운다.
    /// </summary>
    public static class EconomyInstaller
    {
        // 스펙 006 FR-008: 잿골을 여는 건물을 구리 갱도로 변경
        private const string COPPER_PIT_ID = "copper_pit";
        private const string SECOND_HUB_ID = "hub-2";

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
        /// <summary>스토리 048. 선로 개통(잠긴 역).</summary>
        public static IronRing.Track.LineOpener Line { get; private set; }

        private static LineDemoDriver _lineDemo;

        private static EconomyDemoDriver _demo;
        private static TownDemoDriver _townDemo;
        private static IronRing.Items.ItemPlacement _paidPlacement;
        private static IHabitatClusters _lineUnlockClusters;

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

        /// <summary>
        /// 설치된 경제 서비스와 이벤트 구독을 해제한다.
        /// 현재 설치 상태를 사용하며 정적 서비스 참조를 비운다.
        /// </summary>
        private static void Teardown()
        {
            ScreenTicker.Remove(Tick);
            if (_lineUnlockClusters != null) _lineUnlockClusters.HabitatCompleted -= OnHabitatCompleted;
            _lineUnlockClusters = null;
            _demo = null;
            _townDemo = null;
            _lineDemo = null;
            Line?.Dispose();
            Line = null;
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
            Settlement.PartFormats = new[] { Strings.Get("econ_lap_head"), Strings.Get("econ_part_buildings"), Strings.Get("econ_part_fare"),
                Strings.Get("econ_part_upkeep"), Strings.Get("econ_lap_total") };
            Settlement.Settled += ShowSettleToast;
            if (track is IronRing.Track.RingTrack ring)
            {
                Line = new IronRing.Track.LineOpener(ring);
                foreach (string id in Line.LockedHubIds) run?.SetHubOpen(id, false);
                Line.HubOpened += OnHubOpened;
                GameServices.Register<ILineOpener>(Line);
            }
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
            BindLineUnlock();
        }

        /// <summary>
        /// 서식지 완료 신호를 구독하여 황동골 증기역 완성 시 잿골 선로를 열 수 있게 한다.
        /// IHabitatClusters를 사용하며 구독 대상을 _lineUnlockClusters에 저장한다.
        /// </summary>
        private static void BindLineUnlock()
        {
            if (_lineUnlockClusters != null || Line == null) return;
            _lineUnlockClusters = GameServices.Get<IHabitatClusters>();
            if (_lineUnlockClusters != null) _lineUnlockClusters.HabitatCompleted += OnHabitatCompleted;
        }

        /// <summary>
        /// 완성된 서식지의 hubId와 typeId를 확인해 황동골 구리 갱도면 잿골 선로를 연다.
        /// LineOpener.Open의 중복 방지를 사용하며 선로와 마을의 열린 상태를 변경한다.
        /// </summary>
        private static void OnHabitatCompleted(string hubId, string typeId)
        {
            if (hubId == Town?.HubId && typeId == COPPER_PIT_ID) Line?.Open(SECOND_HUB_ID);
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
            Population = new Population(Town, catalog, clusters, GameServices.Get<INpcRecruiter>(), IronRing.Npc.NpcInstaller.Pool)
                { CurrentHub = CurrentHub };
            Tiers = new TownTiers(Town) { CurrentHub = CurrentHub };
            RunState run = GameServices.Get<IHubStateSystem>() as RunState;
            string hub = Town.HubId;
            Population pop = Population;
            TownTiers tiers = Tiers;
            Settlement.Income = () => pop.Income;
            Settlement.AfterSettle = lap =>
            {
                // 스토리 048: 열린 마을마다(이번 정산 중에 열린 마을은 다음 정산부터).
                foreach (string id in new System.Collections.Generic.List<string>(pop.HubIds))
                {
                    float prosperity = run != null ? run.GetProsperity(id) : 0f;
                    tiers.EvaluateAt(id, pop.ResidentsAt(id), prosperity);
                    pop.SettleAt(id, lap);
                    run?.RefreshPhase(id);
                    bool more = tiers.NextNeedsAt(id, out int needResidents, out int needProsperity);
                    Debug.Log($"TOWN: lap={lap} hub={id} tier={tiers.TierAt(id)} {tiers.TierNameAt(id)} residents={pop.ResidentsAt(id)} prosperity={prosperity:0.#} next={(more ? tiers.NameOf(tiers.TierAt(id) + 1) : "-")} need residents {needResidents} prosperity {needProsperity} phase={tiers.PhaseAt(id)}");
                }
            };
            Tiers.HubTierChanged += OnTierChanged;
            Tiers.HubCollapsing += OnCollapsing;
            Tiers.HubUnlockRequested += id => Line?.Open(id);
            AttachTownLink(run, hub);
            // 이미 열린 다른 역(잠기지 않은 역)도 처음부터 센다.
            if (run != null)
                foreach (string id in run.OpenHubIds) if (id != hub) OpenTown(id);
            IronRing.Habitats.HabitatsInstaller.Codex?.BindTiers(Tiers);
            GameServices.Register<IPopulation>(Population);
            GameServices.Register<ITownTier>(Tiers);
            string stage = TownDemoDriver.ReadStage();
            if (stage != null) _townDemo = new TownDemoDriver(stage, Town, Settlement, Population, Tiers, run);
            string lineStage = LineDemoDriver.ReadStage();
            if (lineStage != null) _lineDemo = new LineDemoDriver(lineStage, Town, Settlement, Population, Tiers, run, GameServices.Get<ITrackRuntime>());
            Debug.Log($"TOWN: installed hub={hub} residents={Population.Residents} housing={Population.Housing} light={Population.Light} income={Population.Income} tier={Tiers.TierName} townTiers={run?.TownActive ?? false} demo={stage ?? "none"}");
        }

        /// <summary>스토리 048. 마을 표시 이름: 첫 마을은 TownTiers.json _hubName, 그 밖은 ko.json hub_N(hub-2 → hub_2 "잿골").</summary>
        public static string HubName(string hubId)
        {
            if (Town != null && hubId == Town.HubId && !string.IsNullOrEmpty(Town.HubName)) return Town.HubName;
            return hubId == null ? "" : Strings.Get(hubId.Replace('-', '_'));
        }

        /// <summary>스토리 048. "현재 마을": 열린 내부 화면의 거점 → 열차가 선 (열린) 역 → 첫 마을. 세지 않는 마을이면 Population/TownTiers가 첫 마을로 되돌린다.</summary>
        private static string CurrentHub()
        {
            IViewMode view = GameServices.Get<IViewMode>();
            if (view != null && view.InteriorOpen && !string.IsNullOrEmpty(view.InteriorHubId)) return view.InteriorHubId;
            ITrackRuntime track = GameServices.Get<ITrackRuntime>();
            ITrackNode node = track?.CurrentNode;
            if (track != null && track.IsHeld && node != null && node.Kind == TrackNodeKind.Hub && !node.IsLocked) return node.HubId;
            return Town?.HubId;
        }

        private static void AttachTownLink(RunState run, string id)
        {
            if (run == null || Tiers == null || Population == null) return;
            TownTiers tiers = Tiers; Population pop = Population;
            run.AttachTown(new TownLink
            {
                HubId = id, Phase = () => tiers.PhaseAt(id), Tier = () => tiers.TierAt(id), Residents = () => pop.ResidentsAt(id),
                VictoryTier = Town.VictoryTier, PartialTier = Town.PartialTier,
            });
        }

        /// <summary>스토리 048. 마을 하나를 세기 시작한다(주민·등급·RunState 링크).</summary>
        private static void OpenTown(string hubId)
        {
            if (Population == null || Tiers == null || Population.Tracks(hubId)) return;
            Population.AddHub(hubId);
            Tiers.AddHub(hubId);
            AttachTownLink(GameServices.Get<IHubStateSystem>() as RunState, hubId);
        }

        /// <summary>스토리 048. 역이 열렸다: RunState가 세기 시작, 마을 주민·등급 추가, 토스트 line_opened.</summary>
        private static void OnHubOpened(string hubId)
        {
            (GameServices.Get<IHubStateSystem>() as RunState)?.SetHubOpen(hubId, true);
            OpenTown(hubId);
            ToastScreen.Show(string.Format(Strings.Get("line_opened"), HubName(hubId)), Settings != null ? Settings.SettleToastSeconds : 4f);
        }

        private static void OnTierChanged(string hubId, int tier)
        {
            string name = Tiers.NameOf(tier);
            TownTierData data = Tiers.Data(tier);
            Debug.Log($"TOWN: tier up hub={hubId} {name} ({data.Id}) recipes=[{string.Join(",", data.UnlockRecipes)}] upgrades=[{string.Join(",", data.UnlockUpgrades)}] stops=[{string.Join(",", data.UnlockStops)}] hubs=[{(hubId == Town.HubId ? string.Join(",", data.UnlockHubs) : "")}]");
            ToastScreen.Show(string.Format(Strings.Get("town_tier_up"), HubName(hubId), name), Settings != null ? Settings.SettleToastSeconds : 4f);
        }

        private static void OnCollapsing(string hubId)
        {
            bool primary = hubId == Town.HubId;
            Debug.Log($"TOWN: collapsed {hubId} primary={primary}");
            ToastScreen.Show(string.Format(Strings.Get(primary ? "town_collapsed" : "town_collapsed_bonus"), HubName(hubId)), Settings != null ? Settings.SettleToastSeconds : 4f);
        }

        private static void Tick()
        {
            float dt = Time.deltaTime;
            _demo?.Tick(dt);
            _townDemo?.Tick(dt);
            _lineDemo?.Tick(dt);
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
