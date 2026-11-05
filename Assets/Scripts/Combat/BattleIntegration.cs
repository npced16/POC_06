using System;
using System.Collections.Generic;

using IronRing.Contracts;
namespace IronRing.Combat
{
    /// <summary>기존 마을·NPC 서비스에서 포탑 해금과 승객의 두 배 강화 상태를 수집한다.</summary>
    public static class BattleIntegration
    {
        /// <summary>모든 hubIds의 완성 habitat과 합류 승객을 읽어 현재 마을 성장의 전투 입력을 만든다.</summary>
        public static BattleLoadout CreateLoadout(BattleRules rules, IHabitatClusters clusters,
            INpcRecruiter recruiter, IEnumerable<string> hubIds)
        {
            if (rules == null) throw new ArgumentNullException(nameof(rules));
            var built = new HashSet<string>(StringComparer.Ordinal);
            var joined = new HashSet<string>(StringComparer.Ordinal);
            if (clusters != null && hubIds != null)
                foreach (string hubId in hubIds)
                    foreach (HabitatCluster cluster in clusters.CompletedAt(hubId))
                        if (cluster != null) built.Add(cluster.TypeId);
            if (recruiter != null)
                foreach (string habitatId in built)
                    if (recruiter.JoinedOf(habitatId).Count > 0) joined.Add(habitatId);
            return new BattleLoadout(rules, built, joined);
        }

        /// <summary>승패의 마르크를 기존 지갑에 지급하고 승리 시 확률에 따라 설계 힌트와 승객 후보를 반환한다.</summary>
        public static BattleReward ResolveReward(BattleRules rules, bool victory, IMoney money,
            IHabitatCodex codex, IHabitatClusters clusters, IPassengers passengers, string waypointId,
            System.Random random)
        {
            if (rules == null) throw new ArgumentNullException(nameof(rules));
            if (random == null) throw new ArgumentNullException(nameof(random));
            int marks = victory ? rules.Economy.VictoryMarks : rules.Economy.DefeatMarks;
            string hintId = null;
            string passengerId = null;
            if (victory)
            {
                if (clusters != null && clusters.HabitatTypeIds.Count > 0 && random.NextDouble() < rules.Economy.HintChance)
                {
                    hintId = clusters.HabitatTypeIds[random.Next(clusters.HabitatTypeIds.Count)];
                    if ((codex != null && codex.IsUnlocked(hintId)) || !BattleHintJournal.Record(hintId))
                        marks += rules.Economy.DuplicateHintMarks;
                }
                if (passengers != null && random.NextDouble() < rules.Economy.PassengerChance)
                    passengerId = passengers.TryBoardAny(waypointId);
            }
            money?.TryGain(marks, victory ? "battle-victory" : "battle-defeat");
            return new BattleReward(marks, hintId, passengerId);
        }
    }

    /// <summary>전투 뒤 지급한 마르크와 확률 보상의 설계도·승객 id를 담는다.</summary>
    public sealed class BattleReward
    {
        public int Marks { get; }
        public string HintId { get; }
        public string PassengerId { get; }

        /// <summary>획득한 마르크와 힌트·승객 id를 결과 화면에서 읽을 수 있게 저장한다.</summary>
        public BattleReward(int marks, string hintId, string passengerId)
        {
            Marks = marks;
            HintId = hintId;
            PassengerId = passengerId;
        }
    }
}
