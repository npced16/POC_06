using System;

using UnityEngine;

using IronRing.Contracts;
using IronRing.Contracts.Presentation;

namespace IronRing.Items
{
    /// <summary>
    /// 스토리 013 조립기. Bootstrap을 고치지 않고 GameServices.Composing에서 아이템 시스템을 만들어 등록한다.
    /// 스토리 016부터 IHabitatSystem·IHabitatClusters는 HabitatsInstaller(ClusterHabitatSystem)가 등록해 002의 슬롯식 HabitatEvaluator를 덮어쓴다.
    /// Composing이 오지 않으면 Ready에서 한 번 늦게 설치한다(플레이 세션당 최대 한 번).
    /// </summary>
    public static class ItemsInstaller
    {
        private static bool _installed;
        private static bool _warned;
        private static IWaypointResolver _resolver;
        private static EncounterItemRewards _rewards;

        /// <summary>설치된 카탈로그(뷰·HUD가 이름·태그·비용을 읽는다). 설치 전에는 null.</summary>
        public static ItemCatalog Catalog { get; private set; }

        /// <summary>도메인 재로드가 꺼져 있으므로 플레이 진입마다 정적 상태를 지운다.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            GameServices.Composing -= OnComposing;
            GameServices.Ready -= OnReady;
            Teardown();
            _installed = false;
            _warned = false;
            Catalog = null;
        }

        /// <summary>MainRoot의 GameServices.Reset()(SubsystemRegistration)보다 뒤에 구독하도록 씬 로드 직전에 건다.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Subscribe()
        {
            GameServices.Composing -= OnComposing;
            GameServices.Composing += OnComposing;
            GameServices.Ready -= OnReady;
            GameServices.Ready += OnReady;
        }

        private static void OnComposing() => Install(false);

        private static void OnReady() => Install(true);

        /// <summary>등록된 핵심 서비스로 아이템 인벤토리·배치·태그 서식지·보상 연결을 만든다.</summary>
        private static void Install(bool late)
        {
            if (_installed) return;
            IResourceInventory resources = GameServices.Get<IResourceInventory>();
            if (resources == null) return; // 핵심 시스템이 없는 단독 데모 씬
            _installed = true;
            if (late) UnityEngine.Debug.LogWarning("ItemsInstaller: GameServices.Composing이 오지 않아 Ready에서 늦게 설치합니다. 이미 IHabitatSystem을 읽은 구독자는 이전 구현을 씁니다.");

            Catalog = ItemCatalog.Parse(LoadText("Items/ItemCatalog"));
            var inventory = new ItemInventory(Catalog, resources);
            GameServices.Register<IItemInventory>(inventory);

            _resolver = GameServices.Get<IWaypointResolver>();
            if (_resolver != null)
            {
                TextAsset encounters = UnityEngine.Resources.Load<TextAsset>("Story005Encounters");
                _rewards = EncounterItemRewards.Parse(encounters != null ? encounters.text : null, inventory,
                    (itemId, amount) => GrantItem(inventory, itemId, amount));
                _resolver.EncounterEnded += OnEncounterEnded;
            }

            IHubInterior interior = GameServices.Get<IHubInterior>();
            if (interior == null) { WarnOnce("ItemsInstaller: IHubInterior가 등록되지 않아 배치·태그 서식지 연결을 건너뜁니다."); return; }
            if (interior is IronRing.Interior.HubInterior rewardInterior)
                rewardInterior.AttachItemRewardGrant((itemId, amount) => GrantItem(inventory, itemId, amount));
            IInteriorItemSlots slots = interior as IInteriorItemSlots;
            if (slots == null && interior is IronRing.Interior.HubInterior hubInterior) slots = new HubInteriorItemSlots(hubInterior);
            if (slots == null) { WarnOnce("ItemsInstaller: 칸 쓰기(TryPlaceItem/TryRemoveItem)를 지원하지 않는 IHubInterior라 배치를 건너뜁니다."); return; }

            var placement = new ItemPlacement(inventory, interior, slots, CanEdit);
            // 스토리 050: 잠긴 타일은 놓을 수 없다(판정은 도감 한 곳, 도감이 아직 없으면 허용).
            placement.IsUnlocked = id => GameServices.Get<IHabitatCodex>()?.IsTileUnlocked(id) ?? true;
            GameServices.Register<IItemPlacement>(placement);
            // 스토리 016: 서식지는 같은 타일 묶음·패턴 판정(ClusterHabitatSystem)이 IHabitatSystem과 IHabitatClusters를 맡는다.
            // 013의 TagHabitatSystem(태그 합산)은 코드로 보존하되 등록하지 않는다. NpcRecruiter는 Bootstrap의 BindHabitat(HabitatLink)가 새 구현에 연결한다.
            IronRing.Habitats.HabitatsInstaller.Install(interior, placement, Catalog.Contains,
                (interior as IronRing.Interior.HubInterior)?.HubIds);
        }

        /// <summary>내부 화면이 바로 그 거점일 때만 배치·회수를 허용한다(IViewMode가 없으면 허용).</summary>
        private static bool CanEdit(string hubId)
        {
            IViewMode view = GameServices.Get<IViewMode>();
            return view == null || (view.InteriorOpen && view.InteriorHubId == hubId);
        }

        /// <summary>itemId와 amount를 받아 타일은 배치 재고에, 그 외 아이템은 inventory에 지급한다. 재고 서비스가 없으면 기존 인벤토리에 지급한다.</summary>
        private static void GrantItem(IItemInventory inventory, string itemId, int amount)
        {
            ItemDefinition item = Catalog.Get(itemId);
            ITileStock stock = GameServices.Get<ITileStock>();
            if (item != null && item.Category == ItemDefinition.TileCategory && stock != null)
                stock.Add(itemId, amount);
            else
                inventory.Add(itemId, amount);
        }

        /// <summary>조우 종료 시 데이터의 아이템 보상을 지급한다. 이벤트 제목은 005의 WaypointResolver에서 읽는다.</summary>
        private static void OnEncounterEnded(ITrackNode node, EncounterOutcome outcome)
        {
            string title = (_resolver as IronRing.Combat.WaypointResolver)?.EventTitle;
            string granted = _rewards?.Grant(outcome, title);
            if (granted != null) UnityEngine.Debug.Log($"ItemsInstaller: 조우 보상 아이템 {granted} ({outcome}).");
        }

        private static string LoadText(string path)
        {
            TextAsset asset = UnityEngine.Resources.Load<TextAsset>(path);
            if (asset == null) throw new InvalidOperationException("Missing item data resource: " + path);
            return asset.text;
        }

        private static void WarnOnce(string message)
        {
            if (_warned) return;
            _warned = true;
            UnityEngine.Debug.LogWarning(message);
        }

        private static void Teardown()
        {
            if (_resolver != null) _resolver.EncounterEnded -= OnEncounterEnded;
            _resolver = null;
            _rewards = null;
        }

        /// <summary>012의 HubInterior 칸 전이 메서드를 IInteriorItemSlots로 노출하는 어댑터.</summary>
        private sealed class HubInteriorItemSlots : IInteriorItemSlots
        {
            private readonly IronRing.Interior.HubInterior _interior;
            public HubInteriorItemSlots(IronRing.Interior.HubInterior interior) { _interior = interior; }
            public bool TryPlaceItem(string hubId, int cellIndex, string itemId) => _interior.TryPlaceItem(hubId, cellIndex, itemId);
            public bool TryRemoveItem(string hubId, int cellIndex) => _interior.TryRemoveItem(hubId, cellIndex);
        }
    }
}
