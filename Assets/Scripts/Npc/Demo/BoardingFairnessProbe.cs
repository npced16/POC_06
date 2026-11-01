using System;
using System.Collections.Generic;
using System.Text;

using UnityEngine;

namespace IronRing.Npc.Demo
{
    /// <summary>
    /// 스토리 028 (QA S2-2) 검증용 로그 도구. 브리지 execute-method로 플레이 없이 부른다:
    /// IronRing.Npc.Demo.BoardingFairnessProbe.Run. 실제 NpcPool 데이터와 Passengers.BoardingOrder(셔플 + 거점 순환 우선)를 써서
    /// 경유지 굴림을 흉내 낸다(48 경유지 = 12바퀴). 근사: 탑승 조건·출신 경유지 일치는 모두 통과로 보고, 하차는 없으며 인내(바퀴 × 경유지 수) 뒤 떠난다.
    /// 같은 조건에서 옛 규칙(풀 순서)과 나란히 hub별 탑승 수를 찍는다.
    /// </summary>
    public static class BoardingFairnessProbe
    {
        private const int Waypoints = 48;
        private const int WaypointsPerLap = 4;

        /// <summary>NpcPool을 읽어 새 규칙과 옛 규칙으로 각각 Waypoints번 굴리고 결과를 Debug.Log로 남긴다.</summary>
        public static void Run()
        {
            NpcDataRefs refs = Resources.Load<NpcDataRefs>(NpcDataRefs.ResourcePath);
            if (refs == null || refs.Pool == null) { Debug.LogError("BOARD-PROBE: NpcPool을 찾지 못했습니다."); return; }
            NpcPoolData pool = NpcPoolData.FromJson(refs.Pool.text);
            var hubs = new List<string>();
            foreach (NpcDefinition npc in pool.Definitions)
                if (npc.EligibleHubIds.Count > 0 && !hubs.Contains(npc.EligibleHubIds[0])) hubs.Add(npc.EligibleHubIds[0]);
            hubs.Sort(StringComparer.Ordinal);
            Debug.Log("BOARD-PROBE new: " + Simulate(pool, hubs, true));
            Debug.Log("BOARD-PROBE old: " + Simulate(pool, hubs, false));
        }

        private static string Simulate(NpcPoolData pool, List<string> hubs, bool fair)
        {
            var random = new System.Random(pool.PassengerSeed);
            var riders = new List<(NpcDefinition Npc, int Since)>();
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            var log = new StringBuilder();
            int patience = Math.Max(1, pool.PatienceLaps) * WaypointsPerLap;
            int rolls = 0;
            for (int w = 0; w < Waypoints; w++)
            {
                riders.RemoveAll(r => w - r.Since >= patience);
                if (riders.Count >= pool.MaxPassengers) continue; // Passengers.RollBoarding: 빈 좌석이 없으면 순번도 넘기지 않는다
                var candidates = new List<NpcDefinition>();
                foreach (NpcDefinition npc in pool.Definitions)
                    if (npc.Passenger != null && riders.FindIndex(r => r.Npc == npc) < 0) candidates.Add(npc);
                string priority = hubs.Count > 0 ? hubs[rolls++ % hubs.Count] : null;
                List<NpcDefinition> order = fair ? Passengers.BoardingOrder(candidates, priority, random) : candidates;
                foreach (NpcDefinition npc in order)
                {
                    if (riders.Count >= pool.MaxPassengers) break;
                    if (random.NextDouble() >= npc.Passenger.Chance) continue;
                    riders.Add((npc, w));
                    string home = npc.EligibleHubIds.Count > 0 ? npc.EligibleHubIds[0] : "-";
                    counts[home] = (counts.TryGetValue(home, out int n) ? n : 0) + 1;
                    log.Append($" wp{w + 1}({priority}):{npc.Id}@{home}");
                }
            }
            var summary = new StringBuilder();
            foreach (string hub in hubs) summary.Append($"{hub}={(counts.TryGetValue(hub, out int c) ? c : 0)} ");
            return summary + "|" + log;
        }
    }
}
