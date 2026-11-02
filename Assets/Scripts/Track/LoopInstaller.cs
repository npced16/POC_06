using System;
using System.Collections.Generic;

using UnityEngine;

using IronRing.Contracts;
using IronRing.Contracts.Presentation;
using IronRing.Hud.Ui;
using IronRing.Localization;

namespace IronRing.Track
{
    /// <summary>
    /// 스토리 051 조립기. Composing에서 RestEvents.json으로 PhaseLoop(IPhaseLoop·IRestEvents)와 TileStock(ITileStock)을 만들어 등록하고,
    /// Ready에서 ItemPlacement 결제에 재고를 걸고(재고 먼저, 없으면 돈), 옛 휴식지 확률 탑승을 끈다(_disableAutoBoarding).
    /// 로그: PhaseLoop가 "LOOP: phase …"·"LOOP: event …", 여기서 재고가 바뀔 때마다 "LOOP: stock &lt;item&gt;=&lt;n&gt;".
    /// </summary>
    public static class LoopInstaller
    {
        /// <summary>사건 데이터 경로(Resources/Track/RestEvents.json).</summary>
        public const string DataPath = "Track/RestEvents";

        private static IronRing.Track.Demo.LoopDemoDriver _demo;
        private static bool _readyBound;

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
            if (Loop.Data._disableAutoBoarding && GameServices.Get<IPassengers>() is IronRing.Npc.Passengers passengers) passengers.AutoBoarding = false;
            if (IronRing.Track.Demo.LoopDemoDriver.IsRequested())
                _demo = new IronRing.Track.Demo.LoopDemoDriver(Loop, Stock, GameServices.Get<ITrackRuntime>());
        }

        private static void Tick()
        {
            float dt = UnityEngine.Time.deltaTime;
            Loop?.Tick(dt);
            _demo?.Tick(dt);
        }

        private static void OnStockChanged(string itemId, int count) => UnityEngine.Debug.Log($"LOOP: stock {itemId}={count}");

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
            if (Stock != null) Stock.Changed -= OnStockChanged;
            Loop?.Dispose();
            Loop = null;
            Stock = null;
            _demo = null;
            _readyBound = false;
        }
    }
}
