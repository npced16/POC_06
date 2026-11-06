using System;
using System.Collections.Generic;

using UnityEngine;

using IronRing.Contracts;
using IronRing.Contracts.Presentation;
using IronRing.Hud.Npc;
using IronRing.Hud.Ui;
using IronRing.Localization;

namespace IronRing.Track
{
    /// <summary>
    /// 스토리 051 조립기. Composing에서 RestEvents.json으로 PhaseLoop(IPhaseLoop·IRestEvents)와 TileStock(ITileStock)을 만들어 등록하고,
    /// Ready에서 ItemPlacement에 재고를 연결하고 바퀴마다 완성 건물의 생산을 정산한다.
    /// 로그: PhaseLoop가 "LOOP: phase …"·"LOOP: event …", 여기서 재고가 바뀔 때마다 "LOOP: stock &lt;item&gt;=&lt;n&gt;".
    /// </summary>
    public static class LoopInstaller
    {
        /// <summary>사건 데이터 경로(Resources/Track/RestEvents.json).</summary>
        public const string DataPath = "Track/RestEvents";

        private static IronRing.Track.Demo.LoopDemoDriver _demo;
        private static bool _readyBound;
        private static ITrackRuntime _productionTrack;
        private static ProductionData _production;
        private static IHabitatCodex _rewardCodex;

        /// <summary>설치된 페이즈 루프(설치 전 null).</summary>
        public static PhaseLoop Loop { get; private set; }
        /// <summary>설치된 타일 재고(설치 전 null).</summary>
        public static IronRing.Items.TileStock Stock { get; private set; }

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

        private static void Install()
        {
            if (Loop != null) return;
            ITrackRuntime track = GameServices.Get<ITrackRuntime>();
            if (track == null) return; // 핵심 시스템이 없는 단독 데모 씬
            TextAsset asset = UnityEngine.Resources.Load<TextAsset>(DataPath);
            if (asset == null) { UnityEngine.Debug.LogError("LOOP: Resources/" + DataPath + ".json 없음"); return; }
            RestEventData data = RestEventData.Parse(asset.text);
            Stock = new IronRing.Items.TileStock();
            Stock.Changed += OnStockChanged;
            Loop = new PhaseLoop(data, track, Stock, Text, ItemName,
                () => GameServices.Get<IHabitatCodex>(), () => GameServices.Get<IMoney>(),
                () => GameServices.Get<IHubStateSystem>(), () => GameServices.Get<IPassengers>());
            GameServices.Register<IPhaseLoop>(Loop);
            GameServices.Register<IRestEvents>(Loop);
            GameServices.Register<ITileStock>(Stock);
            ScreenTicker.Add(Tick);
        }

        private static void OnReady()
        {
            if (Loop == null) Install();
            if (Loop == null || _readyBound) return;
            _readyBound = true;
            if (GameServices.Get<IItemPlacement>() is IronRing.Items.ItemPlacement placement) placement.Stock = Stock;
            else UnityEngine.Debug.LogWarning("LOOP: ItemPlacement가 없어 재고 결제를 걸지 못했습니다.");
            TextAsset productionAsset = UnityEngine.Resources.Load<TextAsset>("Habitats/HabitatProduction");
            if (productionAsset != null) _production = JsonUtility.FromJson<ProductionData>(productionAsset.text);
            else UnityEngine.Debug.LogError("LOOP: Resources/Habitats/HabitatProduction.json 없음");
            _productionTrack = GameServices.Get<ITrackRuntime>();
            if (_productionTrack != null) _productionTrack.LapCompleted += Produce;
            _rewardCodex = GameServices.Get<IHabitatCodex>();
            if (_rewardCodex != null) _rewardCodex.RecipeUnlocked += GrantFirstCompletion;
            if (Loop.Data._disableAutoBoarding && GameServices.Get<IPassengers>() is IronRing.Npc.Passengers passengers) passengers.AutoBoarding = false;
            if (IronRing.Track.Demo.LoopDemoDriver.IsRequested())
                _demo = new IronRing.Track.Demo.LoopDemoDriver(Loop, Stock, GameServices.Get<ITrackRuntime>());
        }

        /// <summary>
        /// 프레임 시간으로 사건 루프와 데모를 갱신한다.
        /// 승객 대화가 열려 있으면 사건 자동 진행 시간을 멈춘다.
        /// </summary>
        private static void Tick()
        {
            float dt = UnityEngine.Time.deltaTime;
            Loop?.Tick(NpcDialogScreen.Instance?.IsOpen == true ? 0f : dt);
            _demo?.Tick(dt);
        }

        private static void OnStockChanged(string itemId, int count) => UnityEngine.Debug.Log($"LOOP: stock {itemId}={count}");

        /// <summary>완료 바퀴 lap에서 모든 마을의 완성 생산 건물을 집계해 재료 재고를 늘린다.</summary>
        private static void Produce(int lap)
        {
            IHubStateSystem hubs = GameServices.Get<IHubStateSystem>();
            IHabitatClusters clusters = GameServices.Get<IHabitatClusters>();
            if (hubs == null || clusters == null || _production?._entries == null) return;
            var visited = new HashSet<string>(StringComparer.Ordinal);
            foreach (string hubId in hubs.HubIds)
            {
                if (string.IsNullOrEmpty(hubId) || !visited.Add(hubId)) continue;
                foreach (HabitatCluster cluster in clusters.CompletedAt(hubId))
                {
                    foreach (ProductionEntry entry in _production._entries)
                    {
                        if (entry._habitatId != cluster.TypeId || entry._amount <= 0 || string.IsNullOrEmpty(entry._itemId)) continue;
                        Stock.Add(entry._itemId, entry._amount);
                    }
                }
            }
        }

        /// <summary>처음 발견한 typeId의 생산물 또는 조합 재료를 찾아 재고에 4개 지급한다. 저장 복원과 다른 해금 출처는 무시한다.</summary>
        private static void GrantFirstCompletion(string typeId, string source)
        {
            if (source != IronRing.Habitats.HabitatCodex.SOURCE_DISCOVER || IronRing.Save.SaveSlots.IsRestoring) return;
            IHubStateSystem hubs = GameServices.Get<IHubStateSystem>();
            IHabitatClusters clusters = GameServices.Get<IHabitatClusters>();
            bool completed = false;
            if (hubs != null && clusters != null)
                foreach (string hubId in hubs.HubIds)
                    foreach (HabitatCluster cluster in clusters.CompletedAt(hubId))
                        if (cluster.TypeId == typeId) { completed = true; break; }
            if (!completed) return;
            string itemId = null;
            if (_production?._entries != null)
                foreach (ProductionEntry entry in _production._entries)
                    if (entry != null && entry._habitatId == typeId && entry._amount > 0 && !string.IsNullOrEmpty(entry._itemId))
                    { itemId = entry._itemId; break; }
            if (itemId == null)
            {
                IronRing.Habitats.HabitatType type = IronRing.Habitats.HabitatsInstaller.Catalog?.Get(typeId);
                if (type != null)
                    foreach (string tile in type.TileIds())
                        if (!string.IsNullOrEmpty(tile) && tile != "tile_cobble") { itemId = tile; break; }
            }
            if (itemId != null) Stock.Add(itemId, 4);
            else UnityEngine.Debug.LogWarning("LOOP: 첫 완성 보상 재료 없음 " + typeId);
        }

        [Serializable]
        private sealed class ProductionData
        {
            public ProductionEntry[] _entries;
        }

        [Serializable]
        private sealed class ProductionEntry
        {
            public string _habitatId;
            public string _itemId;
            public int _amount;
        }

        /// <summary>ko.json 키 → 한국어(없는 키는 키 그대로, 빈 값은 "").</summary>
        private static string Text(string key)
        {
            if (string.IsNullOrEmpty(key)) return "";
            try { return Strings.Get(key); }
            catch (KeyNotFoundException) { UnityEngine.Debug.LogWarning("LOOP: ko.json 키 없음 " + key); return key; }
        }

        private static string ItemName(string itemId) => IronRing.Items.ItemsInstaller.Catalog?.Get(itemId)?.Name ?? itemId;

        private static void Teardown()
        {
            ScreenTicker.Remove(Tick);
            if (_productionTrack != null) _productionTrack.LapCompleted -= Produce;
            if (_rewardCodex != null) _rewardCodex.RecipeUnlocked -= GrantFirstCompletion;
            _rewardCodex = null;
            _productionTrack = null;
            _production = null;
            if (Stock != null) Stock.Changed -= OnStockChanged;
            Loop?.Dispose();
            Loop = null;
            Stock = null;
            _demo = null;
            _readyBound = false;
        }
    }
}
