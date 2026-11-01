using UnityEngine;

using IronRing.Contracts;
using IronRing.Contracts.Presentation;

namespace IronRing.Hub
{
    /// <summary>
    /// Story 014 composition without editing Bootstrap. Statics reset in SubsystemRegistration; Composing and Ready are
    /// subscribed in BeforeSceneLoad (after MainRoot's GameServices.Reset). Installs once: on Composing when Bootstrap
    /// raises it, otherwise on Ready (before views that subscribe in AfterSceneLoad).
    /// </summary>
    public static class HubDevelopmentInstaller
    {
        private static bool _installed;

        /// <summary>Clears install state and stale subscriptions for a new play session (domain reload disabled).</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _installed = false;
            GameServices.Composing -= Install;
            GameServices.Ready -= Install;
        }

        /// <summary>Subscribes the installer to both composition hooks without duplicates.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Boot()
        {
            GameServices.Composing -= Install;
            GameServices.Composing += Install;
            GameServices.Ready -= Install;
            GameServices.Ready += Install;
        }

        /// <summary>Attaches development sources to MainRoot's RunState and registers IHubDevelopment and IRunLossInfo once.</summary>
        private static void Install()
        {
            if (_installed) return;
            if (!(GameServices.Get<IHubStateSystem>() is RunState hubs)) return;
            // Story 016: habitat prosperity is summed per completed cluster (IHabitatClusters, catalog bonus per type).
            hubs.AttachSources(new ServiceProsperitySources(GameServices.Get<IHubInterior>, GameServices.Get<IHabitatSystem>,
                GameServices.Get<IHabitatClusters>, IronRing.Habitats.HabitatsInstaller.BonusOf, typeId => CellCapOf(typeId, hubs.CellCapRatio)));
            GameServices.Register<IHubDevelopment>(hubs);
            GameServices.Register<IRunLossInfo>(hubs);
            GameServices.Register<IRunResult>(hubs); // Story 028
            GameServices.Register<IProsperityForecast>(hubs); // Story 028
            _installed = true;
        }

        /// <summary>
        /// QA 2026-11-01 (mixed-cluster prosperity cap): the most cells a completed building of typeId counts for prosperity.
        /// Pattern = its template cell count; Cluster/Set = ceil(RequiredCount x ratio); unknown type = -1 (no cap).
        /// </summary>
        private static int CellCapOf(string typeId, float ratio)
        {
            IronRing.Habitats.HabitatType type = IronRing.Habitats.HabitatsInstaller.Catalog?.Get(typeId);
            if (type == null || type.RequiredCount <= 0) return -1;
            if (type.Kind == IronRing.Habitats.HabitatKind.Pattern) return type.RequiredCount;
            return Mathf.CeilToInt(type.RequiredCount * ratio);
        }
    }
}
