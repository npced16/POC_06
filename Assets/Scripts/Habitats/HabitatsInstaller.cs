using System;
using System.Collections.Generic;

using UnityEngine;

using IronRing.Contracts;
using IronRing.Contracts.Presentation;

namespace IronRing.Habitats
{
    /// <summary>
    /// 스토리 016 조립 도우미. ItemsInstaller(013)가 GameServices.Composing 안에서 IItemPlacement를 등록한 직후 Install을 부른다
    /// (같은 Composing 구독 순서에 기대지 않도록 별도 구독하지 않는다). ClusterHabitatSystem을 IHabitatSystem과 IHabitatClusters로 등록한다.
    /// 도메인 재로드가 꺼져 있으므로 정적 상태는 SubsystemRegistration에서 지운다.
    /// </summary>
    public static class HabitatsInstaller
    {
        /// <summary>서식지 카탈로그 리소스 경로(Resources/Habitats/HabitatTypes.json).</summary>
        public const string CatalogPath = "Habitats/HabitatTypes";

        /// <summary>설치된 카탈로그(표현 계층이 이름·보너스를 읽는다). 설치 전에는 null.</summary>
        public static HabitatCatalog Catalog { get; private set; }

        /// <summary>설치된 시스템. 설치 전에는 null.</summary>
        public static ClusterHabitatSystem System { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            System?.Dispose();
            System = null;
            Catalog = null;
        }

        /// <summary>
        /// 카탈로그를 읽고 시스템을 만들어 GameServices에 IHabitatSystem·IHabitatClusters로 등록한다. isKnownTile로 카탈로그의 타일 id를
        /// 검사해 모르는 id는 경고한다(그 종류는 완성될 수 없을 뿐 동작은 계속). hubIds는 초기 판정용(없으면 null).
        /// </summary>
        public static ClusterHabitatSystem Install(IHubInterior interior, IItemPlacement placement, Func<string, bool> isKnownTile, IEnumerable<string> hubIds)
        {
            if (interior == null) throw new ArgumentNullException(nameof(interior));
            System?.Dispose();
            TextAsset asset = UnityEngine.Resources.Load<TextAsset>(CatalogPath);
            if (asset == null) throw new InvalidOperationException("Missing habitat catalog resource: " + CatalogPath);
            Catalog = HabitatCatalog.Parse(asset.text);
            List<string> unknown = Catalog.UnknownTiles(isKnownTile);
            if (unknown.Count > 0) UnityEngine.Debug.LogWarning("HabitatsInstaller: 아이템 카탈로그에 없는 타일 id: " + string.Join(", ", unknown));
            System = new ClusterHabitatSystem(Catalog, interior, placement, hubIds);
            GameServices.Register<IHabitatSystem>(System);
            GameServices.Register<IHabitatClusters>(System);
            return System;
        }

        /// <summary>종류 id의 카탈로그 번영 보너스(모르는 id나 설치 전이면 0 = 거점 기본값 사용). 014 번영 규칙이 쓴다.</summary>
        public static float BonusOf(string typeId) => Catalog?.Get(typeId)?.ProsperityBonus ?? 0f;
    }
}
