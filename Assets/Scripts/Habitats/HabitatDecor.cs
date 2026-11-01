using System;
using System.Collections.Generic;

namespace IronRing.Habitats
{
    /// <summary>
    /// QA 후속(2026-11-01 "건물마다 다른 장식"). 완성 건물의 꾸밈 묶음: 지붕 색조 id, 정면 창 무늬, 지붕 위 덧소품 1~2개.
    /// HabitatTypes.json 종류마다 선택 블록 _decor({"_roofTint", "_windows", "_props"})에서 오며, 없으면 _buildingStyle별 기본값을 쓴다.
    /// 표현(BuildingView)이 종류마다 같은 값으로 그리므로 결과는 결정적이다(난수 없음).
    /// </summary>
    public sealed class HabitatDecor
    {
        /// <summary>기본 지붕 색(색조 없음)을 뜻하는 id.</summary>
        public const string BaseTint = "base";

        /// <summary>지붕 색조 id(base|copper|wine|moss, SpriteGenBuildings.TINTS). 모르는 id면 표현이 기본 지붕을 쓴다.</summary>
        public string RoofTint { get; }
        /// <summary>정면 창 무늬(0 = 지붕 조각의 기본 창, 1 = 덧창+꽃상자, 2 = 둥근 현창).</summary>
        public int WindowPattern { get; }
        /// <summary>지붕 위 덧소품 id들(SpriteGenBuildings.DECOR, 데이터 순서, 최대 2개).</summary>
        public IReadOnlyList<string> Props { get; }

        /// <summary>값을 정리해 만든다(빈 색조는 base, 음수 창은 0, 빈 소품 id는 버리고 앞의 2개만 남긴다).</summary>
        public HabitatDecor(string roofTint, int windowPattern, IEnumerable<string> props)
        {
            RoofTint = string.IsNullOrWhiteSpace(roofTint) ? BaseTint : roofTint.Trim();
            WindowPattern = Math.Max(0, windowPattern);
            var list = new List<string>();
            foreach (string id in props ?? Array.Empty<string>())
                if (!string.IsNullOrWhiteSpace(id) && list.Count < 2) list.Add(id.Trim());
            Props = list.AsReadOnly();
        }

        /// <summary>_decor 블록이 없을 때 지붕 계열별 기본 꾸밈.</summary>
        public static HabitatDecor DefaultFor(string buildingStyle)
        {
            switch (buildingStyle)
            {
                case "machine": return new HabitatDecor(BaseTint, 2, new[] { "steam_pipes" });
                case "storage": return new HabitatDecor(BaseTint, 0, new[] { "coal_pile" });
                case "glass": return new HabitatDecor(BaseTint, 1, new[] { "glass_cap" });
                default: return new HabitatDecor(BaseTint, 1, new[] { "laundry" });
            }
        }
    }
}
