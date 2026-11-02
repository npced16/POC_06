using IronRing.Contracts;
using IronRing.Localization;

namespace IronRing.Hud.Ui
{
    /// <summary>마을 등급 문구와 주민·번영 조건을 화면에 맞게 구성한다.</summary>
    public static class TownUiText
    {
        /// <summary>마을 설정을 못 읽을 때 쓰는 등급 거점 id.</summary>
        public const string DEFAULT_HUB = "hub-1";

        /// <summary>현재 마을 id를 반환하며, 서비스가 없으면 기본 hub-1을 반환한다.</summary>
        public static string HubId(IPopulation population)
        {
            return population != null && !string.IsNullOrEmpty(population.CurrentHubId) ? population.CurrentHubId : DEFAULT_HUB;
        }

        /// <summary>"목표: 도시 (주민 a/b · 번영 c/d) · 바퀴 e/f". 승리 등급 데이터를 못 읽으면 null.</summary>
        public static string Goal(ITownTier tier, int residents, int prosperity, int lap, int lapTotal)
        {
            if (!(tier is IronRing.Economy.TownTiers data)) return null;
            IronRing.Economy.TownTierData goal = data.Data(data.VictoryTier);
            string laps = lapTotal > 0 ? string.Format(Strings.Get("ui_goal_laps"), System.Math.Min(lap, lapTotal), lapTotal) : "";
            return string.Format(Strings.Get("ui_goal_line"), goal.Name, residents, goal.Residents, prosperity, goal.Prosperity) + laps;
        }

        /// <summary>로직이 띄우는 등급 상승 토스트(town_tier_up)가 tierName에 대한 것인지 판별하는 함수.</summary>
        public static System.Predicate<string> TierToastMatcher(string tierName)
        {
            string sample = string.Format(Strings.Get("town_tier_up"), "", tierName);
            int cut = sample.IndexOf('');
            string tail = cut >= 0 ? sample.Substring(cut + 1) : sample;
            return text => text != null && text.EndsWith(tail);
        }

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

        /// <summary>마을별 등급과 다음 조건을 hubId 기준으로 표시한다. hubId, 주민 수, 번영을 사용하며 다음 조건 문구를 반환한다.</summary>
        public static string NextAt(ITownTier tier, string hubId, int residents, int prosperity)
        {
            if (!tier.NextNeedsAt(hubId, out int residentsNeed, out int prosperityNeed)) return Strings.Get("ui_tier_max");
            return string.Format(Strings.Get("ui_tier_next"), Name(tier, tier.TierAt(hubId) + 1), residents, residentsNeed, prosperity, prosperityNeed);
        }
    }
}
