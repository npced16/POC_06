using System;
using System.Collections.Generic;

using UnityEngine;

using IronRing.Habitats;

namespace IronRing.Shop
{
    /// <summary>
    /// 스펙 010. Resources/Shop/FragmentShop.json의 진열 수와 건물별 조각 가격.
    /// 잘못된 가격이나 카탈로그에 없는 건물 id가 있으면 오류를 출력하고 IsValid를 false로 둬 결제를 막는다.
    /// </summary>
    public sealed class FragmentShopSettings
    {
        private readonly Dictionary<string, int> _prices = new Dictionary<string, int>(StringComparer.Ordinal);

        /// <summary>한 바퀴에 진열하는 최대 건물 수.</summary>
        public int OfferCount { get; private set; }
        /// <summary>데이터 검사를 통과했으면 true. false면 상점은 어떤 구매도 허용하지 않는다.</summary>
        public bool IsValid { get; private set; }
        /// <summary>건물 id별 조각 가격.</summary>
        public IReadOnlyDictionary<string, int> Prices => _prices;

        /// <summary>
        /// json의 진열 수와 가격 목록을 읽고 catalog로 건물 id를 검사한다.
        /// 검사 결과를 IsValid에 담은 설정을 반환하며, 문제가 있으면 Debug.LogError로 알린다.
        /// </summary>
        public static FragmentShopSettings Parse(string json, HabitatCatalog catalog)
        {
            var settings = new FragmentShopSettings { IsValid = true };
            var errors = new List<string>();
            ShopData data = string.IsNullOrWhiteSpace(json) ? null : JsonUtility.FromJson<ShopData>(json);
            if (data == null || data._prices == null)
            {
                errors.Add("가격 목록이 없습니다");
                data = new ShopData { _prices = Array.Empty<PriceData>() };
            }
            settings.OfferCount = data._offerCount;
            if (settings.OfferCount < 1) errors.Add("진열 수는 1 이상이어야 합니다: " + data._offerCount);
            foreach (PriceData price in data._prices)
            {
                string id = price?._recipeId;
                if (string.IsNullOrWhiteSpace(id) || catalog?.Get(id) == null) { errors.Add("카탈로그에 없는 건물 id: " + id); continue; }
                if (price._price <= 0) { errors.Add("가격은 양의 정수여야 합니다: " + id + "=" + price._price); continue; }
                if (!settings._prices.TryAdd(id, price._price)) errors.Add("가격이 중복되었습니다: " + id);
            }
            if (catalog != null)
            {
                foreach (HabitatType type in catalog.Types)
                    if (!settings._prices.ContainsKey(type.Id)) Debug.LogWarning("FragmentShop: 가격이 없어 판매하지 않는 건물: " + type.Id);
            }
            if (errors.Count == 0) return settings;
            settings.IsValid = false;
            Debug.LogError("FragmentShop: 상점 데이터를 읽지 못해 구매를 막습니다. " + string.Join(" / ", errors));
            return settings;
        }

#pragma warning disable 0649
        [Serializable] private sealed class ShopData { public int _offerCount = 4; public PriceData[] _prices; }
        [Serializable] private sealed class PriceData { public string _recipeId; public int _price; }
#pragma warning restore 0649
    }
}
