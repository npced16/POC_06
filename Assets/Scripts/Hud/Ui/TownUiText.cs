using IronRing.Contracts;
using IronRing.Localization;

namespace IronRing.Hud.Ui
{
    public static class TownUiText
    {
        /// <summary>tier의 number 등급 이름을 데이터에서 읽고 데이터가 없으면 지역화 이름을 반환한다.</summary>
        public static string Name(ITownTier tier, int number)
        {
            return tier is IronRing.Economy.TownTiers data ? data.NameOf(number) : Strings.Get("ui_tier_name_" + number);
        }

        /// <summary>tier·residents·prosperity로 다음 등급 조건을 만들며 최고 등급이면 달성 문구를 반환한다.</summary>
        public static string Next(ITownTier tier, int residents, int prosperity)
        {
            if (tier.IsMax) return Strings.Get("ui_tier_max");
            return string.Format(Strings.Get("ui_tier_next"), Name(tier, tier.NextTier), residents, tier.NextResidentsNeed, prosperity, tier.NextProsperityNeed);
        }
    }
}
