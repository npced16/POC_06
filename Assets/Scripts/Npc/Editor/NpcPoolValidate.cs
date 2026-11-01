using System.Collections.Generic;
using System.Linq;

using UnityEditor;
using UnityEngine;

using IronRing.Habitats;

namespace IronRing.Npc.Editor
{
    /// <summary>
    /// 스토리 030. NPC 풀 데이터 검증: 서식지 19종이 모두 적어도 한 명의 NPC(원하는 것 = 탑승 해금, 가르치는 것 = 합류 해금) 또는 시작 레시피로
    /// 열릴 수 있는지, 그리고 종류마다 몇 개 거점의 NPC가 여는지 로그로 남긴다. 가르치는 종류가 자기 서식지와 같거나 카탈로그에 없으면 경고.
    /// 브리지: execute-method --method IronRing.Npc.Editor.NpcPoolValidate.Run
    /// </summary>
    public static class NpcPoolValidate
    {
        private const string PoolPath = "Assets/Scripts/Npc/Data/NpcPool.json";
        private const string CatalogResource = "Habitats/HabitatTypes";
        private const string UnlocksResource = "Habitats/CodexUnlocks";

        /// <summary>검증을 돌리고 "NPC POOL: all N types reachable" 또는 빠진 종류를 로그한다.</summary>
        [MenuItem("IronRing/Validate NPC Pool (030)")]
        public static void Run()
        {
            NpcPoolData pool = NpcPoolData.FromJson(AssetDatabase.LoadAssetAtPath<TextAsset>(PoolPath).text);
            HabitatCatalog catalog = HabitatCatalog.Parse(Resources.Load<TextAsset>(CatalogResource).text);
            CodexUnlockSettings unlocks = CodexUnlockSettings.Parse(Resources.Load<TextAsset>(UnlocksResource).text);
            var hubsOf = new Dictionary<string, HashSet<string>>();
            var byWhom = new Dictionary<string, List<string>>();
            foreach (HabitatType type in catalog.Types) { hubsOf[type.Id] = new HashSet<string>(); byWhom[type.Id] = new List<string>(); }
            int warnings = 0;
            foreach (NpcDefinition npc in pool.Definitions)
            {
                if (npc.TeachesTypeId == null) { Debug.LogWarning($"NPC POOL: {npc.Id} 가르치는 종류 없음"); warnings++; }
                else if (npc.TeachesTypeId == npc.HabitatTypeId) { Debug.LogWarning($"NPC POOL: {npc.Id} 가르치는 종류가 자기 서식지와 같음({npc.TeachesTypeId})"); warnings++; }
                foreach (string typeId in new[] { npc.HabitatTypeId, npc.TeachesTypeId })
                {
                    if (typeId == null) continue;
                    if (!hubsOf.ContainsKey(typeId)) { Debug.LogWarning($"NPC POOL: {npc.Id} 카탈로그에 없는 종류 {typeId}"); warnings++; continue; }
                    foreach (string hub in npc.EligibleHubIds) hubsOf[typeId].Add(hub);
                    byWhom[typeId].Add((typeId == npc.TeachesTypeId ? "teach:" : "want:") + npc.Id);
                }
            }
            var missing = new List<string>();
            var single = new List<string>();
            foreach (HabitatType type in catalog.Types)
            {
                bool starter = unlocks.Starters.Contains(type.Id);
                int hubs = hubsOf[type.Id].Count;
                if (hubs == 0 && !starter) missing.Add(type.Id);
                else if (hubs < 2) single.Add(type.Id);
                Debug.Log($"NPC POOL: {type.Id}{(starter ? " (start)" : "")} hubs={string.Join(",", hubsOf[type.Id].OrderBy(h => h))} by={string.Join(" ", byWhom[type.Id])}");
            }
            if (single.Count > 0) Debug.Log("NPC POOL: one hub only: " + string.Join(", ", single));
            if (missing.Count == 0) Debug.Log($"NPC POOL: all {catalog.Types.Count} types reachable (npcs={pool.Definitions.Count}, warnings={warnings})");
            else Debug.LogWarning("NPC POOL: unreachable types: " + string.Join(", ", missing));
        }
    }
}
