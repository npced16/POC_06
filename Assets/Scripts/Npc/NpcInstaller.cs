using System;

using UnityEngine;

using IronRing.Contracts;
using IronRing.Contracts.Presentation;

namespace IronRing.Npc
{
    /// <summary>
    /// 스토리 017 조립기. GameServices.Composing에서 NpcHomes와 NpcInteraction을 만들어 INpcHomes·INpcInteraction으로 등록한다.
    /// 스토리 023: 등록된 INpcRecruiter가 NpcRecruiter면 Passengers(열차 승객)를 만들어 IPassengers로 등록하고, Ready에서 토스트 알림을 만든다.
    /// IHabitatClusters·IItemPlacement·IHubDevelopment는 같은 Composing의 다른 설치자가 나중에 등록할 수 있어 늦게 얻고, Ready에서 한 번 더 Bind한다.
    /// 도메인 재로드가 꺼져 있으므로 정적 상태는 SubsystemRegistration에서 지운다.
    /// </summary>
    public static class NpcInstaller
    {
        private const string PoolAssetPath = "Assets/Scripts/Npc/Data/NpcPool.json";
        private const string InteractionAssetPath = "Assets/Scripts/Npc/Data/NpcInteraction.json";
        private static PassengerAnnouncer _announcer;

        /// <summary>설치된 집 시스템. 설치 전에는 null.</summary>
        public static NpcHomes Homes { get; private set; }
        /// <summary>설치된 대화·상호작용 시스템. 설치 전에는 null.</summary>
        public static NpcInteraction Interaction { get; private set; }
        /// <summary>설치에 쓴 NPC 풀(직업명·대사·서식지 종류·승객 데이터). 설치 전에는 null.</summary>
        public static NpcPoolData Pool { get; private set; }
        /// <summary>스토리 023 열차 승객 시스템. 설치 전이거나 단독 데모 씬이면 null.</summary>
        public static Passengers Passengers { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            GameServices.Composing -= OnComposing;
            GameServices.Ready -= OnReady;
            Homes?.Dispose();
            Interaction?.Dispose();
            Passengers?.Dispose();
            Homes = null;
            Interaction = null;
            Passengers = null;
            Pool = null;
            _announcer = null;
        }

        /// <summary>MainRoot의 GameServices.Reset()(SubsystemRegistration)보다 뒤에 구독하도록 씬 로드 직전에 건다.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Subscribe()
        {
            GameServices.Composing -= OnComposing;
            GameServices.Composing += OnComposing;
            GameServices.Ready -= OnReady;
            GameServices.Ready += OnReady;
        }

        private static void OnComposing() => Install();

        private static void OnReady()
        {
            Install();
            Homes?.Bind();
            if (Passengers != null && _announcer == null) _announcer = PassengerAnnouncer.Create(Passengers);
        }

        private static void Install()
        {
            if (Homes != null) return;
            INpcRecruiter recruiter = GameServices.Get<INpcRecruiter>();
            IHubStateSystem hubs = GameServices.Get<IHubStateSystem>();
            if (recruiter == null || hubs == null) return; // 핵심 시스템이 없는 단독 데모 씬
            try
            {
                LoadData(out string poolJson, out string interactionJson);
                Pool = NpcPoolData.FromJson(poolJson);
                NpcInteraction.Settings settings = NpcInteraction.Settings.FromJson(interactionJson);
                Homes = new NpcHomes(recruiter, GameServices.Get<IHabitatClusters>, Pool, hubs.HubIds, GameServices.Get<IItemPlacement>);
                NpcHomes homes = Homes;
                Interaction = new NpcInteraction(Pool, settings, hubs, recruiter, () => homes, GameServices.Get<ITrackRuntime>());
                GameServices.Register<INpcHomes>(Homes);
                GameServices.Register<INpcInteraction>(Interaction);
                if (recruiter is NpcRecruiter concrete)
                {
                    Passengers = new Passengers(Pool, concrete, GameServices.Get<ITrackRuntime>(), GameServices.Get<IWaypointResolver>(),
                        GameServices.Get<IHabitatClusters>, hubs, GameServices.Get<IHubDevelopment>);
                    GameServices.Register<IPassengers>(Passengers);
                }
                else UnityEngine.Debug.Log("NpcInstaller: INpcRecruiter가 NpcRecruiter가 아니라(데모 세계) 열차 승객을 설치하지 않습니다.");
                if (GameServices.Get<IHabitatClusters>() == null)
                    UnityEngine.Debug.Log("NpcInstaller: IHabitatClusters가 아직 없어 집 배정은 등록되는 즉시(늦어도 Ready) 연결합니다.");
            }
            catch (Exception e)
            {
                Homes?.Dispose();
                Passengers?.Dispose();
                Homes = null;
                Interaction = null;
                Passengers = null;
                UnityEngine.Debug.LogError("NpcInstaller: NPC 집·대화·승객 설치 실패 - " + e.Message);
            }
        }

        /// <summary>Resources/Npc/NpcData.asset이 참조하는 두 JSON을 읽는다(에디터에서는 에셋이 없으면 경로로 대신 읽는다).</summary>
        private static void LoadData(out string poolJson, out string interactionJson)
        {
            NpcDataRefs refs = UnityEngine.Resources.Load<NpcDataRefs>(NpcDataRefs.ResourcePath);
            poolJson = refs != null && refs.Pool != null ? refs.Pool.text : null;
            interactionJson = refs != null && refs.Interaction != null ? refs.Interaction.text : null;
#if UNITY_EDITOR
            if (poolJson == null) poolJson = UnityEditor.AssetDatabase.LoadAssetAtPath<TextAsset>(PoolAssetPath)?.text;
            if (interactionJson == null) interactionJson = UnityEditor.AssetDatabase.LoadAssetAtPath<TextAsset>(InteractionAssetPath)?.text;
#endif
            if (poolJson == null) throw new InvalidOperationException("NPC 풀 데이터 없음: Resources/" + NpcDataRefs.ResourcePath + " 또는 " + PoolAssetPath);
            if (interactionJson == null) throw new InvalidOperationException("상호작용 데이터 없음: Resources/" + NpcDataRefs.ResourcePath + " 또는 " + InteractionAssetPath);
        }
    }
}
