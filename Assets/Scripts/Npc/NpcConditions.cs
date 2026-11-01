using System;
using System.Collections.Generic;
using System.Globalization;

using UnityEngine;

using IronRing.Contracts;

namespace IronRing.Npc
{
    /// <summary>Story 023: read-only services a condition may look at. Any member may be null (that condition then fails).</summary>
    public sealed class NpcConditionContext
    {
        public IHubStateSystem Hubs;
        public ITrackRuntime Track;
        public IHabitatClusters Clusters;
        public INpcRecruiter Recruiter;
        public IHubDevelopment Development;
        /// <summary>Visits of a hub so far (including the current one).</summary>
        public Func<string, int> Visits;
    }

    /// <summary>
    /// Story 023 (story-023-npc-rosters-conditions-arrival.md, AC "숨은 조건 종류"). Static evaluator for NPC boarding and hidden
    /// disembark conditions. Types: prosperityAtLeast, lapAtLeast, visitAtLeast, habitatComplete, npcPresent, phaseAtLeast
    /// (Recovering|Settled). An unknown type or unparsable value logs a warning once and counts as unmet.
    /// </summary>
    public static class NpcConditions
    {
        private static readonly HashSet<string> Warned = new HashSet<string>(StringComparer.Ordinal);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Warned.Clear();

        /// <summary>True when every condition holds for hubId (an empty list always holds).</summary>
        public static bool AllMet(IReadOnlyList<NpcCondition> conditions, string hubId, NpcConditionContext context)
        {
            if (conditions == null) return true;
            foreach (NpcCondition condition in conditions)
                if (!IsMet(condition, hubId, context)) return false;
            return true;
        }

        /// <summary>Evaluates one condition for hubId.</summary>
        public static bool IsMet(NpcCondition condition, string hubId, NpcConditionContext context)
        {
            if (condition == null) return true;
            if (context == null) return false;
            switch (condition.Type)
            {
                case "prosperityAtLeast":
                    return hubId != null && context.Hubs != null && TryFloat(condition, out float p) && context.Hubs.GetProsperity(hubId) >= p;
                case "lapAtLeast":
                    return TryInt(condition, out int lap) && (context.Track?.Lap ?? 0) >= lap;
                case "visitAtLeast":
                    return hubId != null && context.Visits != null && TryInt(condition, out int visits) && context.Visits(hubId) >= visits;
                case "habitatComplete":
                    return IsTypeComplete(context.Clusters, hubId, condition.Value);
                case "npcPresent":
                    if (hubId == null || context.Recruiter == null) return false;
                    foreach (string npc in context.Recruiter.NpcsAt(hubId)) if (npc == condition.Value) return true;
                    return false;
                case "phaseAtLeast":
                    if (hubId == null || context.Development == null) return false;
                    if (!Enum.TryParse(condition.Value, true, out HubPhase wanted)) { Warn("phase:" + condition.Value, "알 수 없는 단계 값 " + condition.Value); return false; }
                    return Rank(context.Development.GetPhase(hubId)) >= Rank(wanted);
                default:
                    Warn("type:" + condition.Type, "알 수 없는 조건 종류 '" + condition.Type + "' - 불충족으로 봅니다.");
                    return false;
            }
        }

        /// <summary>True when a habitat of typeId is complete at hubId now.</summary>
        public static bool IsTypeComplete(IHabitatClusters clusters, string hubId, string typeId)
        {
            if (clusters == null || hubId == null || string.IsNullOrEmpty(typeId)) return false;
            foreach (HabitatCluster cluster in clusters.CompletedAt(hubId))
                if (cluster != null && cluster.TypeId == typeId) return true;
            return false;
        }

        /// <summary>Collapsed counts as low as Ruined: "at least Recovering" means the hub is alive and recovering or better.</summary>
        private static int Rank(HubPhase phase) => phase == HubPhase.Recovering ? 1 : phase == HubPhase.Settled ? 2 : 0;

        private static bool TryFloat(NpcCondition c, out float value)
        {
            if (float.TryParse(c.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out value)) return true;
            Warn("value:" + c, "조건 값을 읽을 수 없음 " + c);
            return false;
        }

        private static bool TryInt(NpcCondition c, out int value)
        {
            if (int.TryParse(c.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out value)) return true;
            Warn("value:" + c, "조건 값을 읽을 수 없음 " + c);
            return false;
        }

        private static void Warn(string key, string message)
        {
            if (Warned.Add(key)) UnityEngine.Debug.LogWarning("NpcConditions: " + message);
        }
    }
}
