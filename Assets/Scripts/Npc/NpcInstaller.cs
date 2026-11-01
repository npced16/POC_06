using System;

using UnityEngine;

using IronRing.Contracts;
using IronRing.Contracts.Presentation;

namespace IronRing.Npc
{
    /// <summary>
    /// 스토리 017 조립기. GameServices.Composing에서 NpcHomes와 NpcInteraction을 만들어 INpcHomes·INpcInteraction으로 등록한다.
    /// IHabitatClusters·IItemPlacement는 같은 Composing의 다른 설치자(ItemsInstaller→HabitatsInstaller)가 나중에 등록할 수 있어
    /// NpcHomes가 늦게 얻고, Ready에서 한 번 더 Bind한다. Composing이 오지 않으면 Ready에서 늦게 설치한다.
    /// 도메인 재로드가 꺼져 있으므로 정적 상태는 SubsystemRegistration에서 지운다.
    /// </summary>
    public static class NpcInstaller
    {
        private const string PoolAssetPath = "Assets/Scripts/Npc/Data/NpcPool.json";
        private const string InteractionAssetPath = "Assets/Scripts/Npc/Data/NpcInteraction.json";

        /// <summary>설치된 집 시스템. 설치 전에는 null.</summary>
        public static NpcHomes Homes { get; private set; }
        /// <summary>설치된 대화·상호작용 시스템. 설치 전에는 null.</summary>
        public static NpcInteraction Interaction { get; private set; }
        /// <summary>설치에 쓴 NPC 풀(직업명·대사·서식지 종류). 설치 전에는 null.</summary>
        public static NpcPoolData Pool { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            GameServices.Composing -= OnComposing;
            GameServices.Ready -= OnReady;
            Homes?.Dispose();
            Interaction?.Dispose();
            Homes = null;
            Interaction = null;
            Pool = null;
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
                if (GameServices.Get<IHabitatClusters>() == null)
                    UnityEngine.Debug.Log("NpcInstaller: IHabitatClusters가 아직 없어 집 배정은 등록되는 즉시(늦어도 Ready) 연결합니다.");
            }
            catch (Exception e)
            {
                Homes?.Dispose();
                Homes = null;
                Interaction = null;
                UnityEngine.Debug.LogError("NpcInstaller: NPC 집·대화 설치 실패 - " + e.Message);
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
