using System;
using System.Collections.Generic;

using UnityEngine;

using IronRing.Combat;
using IronRing.Contracts;
using IronRing.Contracts.Presentation;
using IronRing.Habitats;
using IronRing.Localization;

namespace IronRing.Shop
{
    /// <summary>
    /// 스펙 010 조립기. GameServices.Composing에서 FragmentShop.json과 서식지 카탈로그를 직접 읽어 BlueprintShop을 만들고
    /// 새 게임의 첫 진열을 추첨한 뒤 IBlueprintShop으로 등록한다. 저장 슬롯을 불러오면 SaveInstaller가 Ready에서 진열을 덮어쓴다.
    /// ITrackRuntime이 없는 씬에서는 아무것도 하지 않는다. 정적 상태는 SubsystemRegistration에서 지운다.
    /// </summary>
    public static class ShopInstaller
    {
        /// <summary>상점 수치 리소스 경로.</summary>
        public const string SETTINGS_PATH = "Shop/FragmentShop";

        /// <summary>설치된 상점(설치 전 null).</summary>
        public static BlueprintShop Shop { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            GameServices.Composing -= Install;
            Teardown();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Subscribe()
        {
            GameServices.Composing -= Install;
            GameServices.Composing += Install;
        }

        /// <summary>설치된 상점의 구독을 풀고 Shop을 비운다.</summary>
        private static void Teardown()
        {
            Shop?.Dispose();
            Shop = null;
        }

        /// <summary>
        /// 선로가 있으면 상점 데이터와 카탈로그를 읽어 상점을 만들고 첫 진열을 추첨해 IBlueprintShop으로 등록한다.
        /// Shop 정적 참조를 새 상점으로 바꾼다.
        /// </summary>
        private static void Install()
        {
            ITrackRuntime track = GameServices.Get<ITrackRuntime>();
            if (track == null) return;
            Teardown();
            TextAsset types = Resources.Load<TextAsset>(HabitatsInstaller.CatalogPath);
            if (types == null) throw new InvalidOperationException("Missing habitat catalog resource: " + HabitatsInstaller.CatalogPath);
            HabitatCatalog catalog = HabitatCatalog.Parse(types.text);
            TextAsset data = Resources.Load<TextAsset>(SETTINGS_PATH);
            if (data == null) Debug.LogError("FragmentShop: 상점 데이터가 없습니다: Resources/" + SETTINGS_PATH);
            FragmentShopSettings settings = FragmentShopSettings.Parse(data != null ? data.text : null, catalog);
            Shop = new BlueprintShop(settings, catalog, track,
                () => GameServices.Get<IHabitatCodex>(),
                () => GameServices.Get<IMoney>(),
                () => GameServices.Get<ILedger>(),
                BattleHintJournal.Has, Text, new System.Random());
            Shop.Draw();
            GameServices.Register<IBlueprintShop>(Shop);
        }

        /// <summary>ko.json에서 key 문구를 찾아 반환하고 없으면 key를 그대로 반환한다.</summary>
        private static string Text(string key)
        {
            try { return Strings.Get(key); }
            catch (KeyNotFoundException) { return key; }
        }
    }
}
