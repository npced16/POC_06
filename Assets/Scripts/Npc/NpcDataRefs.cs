using UnityEngine;

namespace IronRing.Npc
{
    /// <summary>
    /// 스토리 017. Npc/Data의 JSON(NpcPool.json, NpcInteraction.json)은 Resources 밖에 있어(씬·빌드 스크립트가 그 경로를 참조) 런타임에
    /// 경로로 읽을 수 없다. 이 에셋(Resources/Npc/NpcData.asset)이 두 TextAsset을 참조해 NpcInstaller가 Resources.Load로 얻게 한다.
    /// </summary>
    [CreateAssetMenu(menuName = "IronRing/Npc Data Refs", fileName = "NpcData")]
    public sealed class NpcDataRefs : ScriptableObject
    {
        /// <summary>Resources.Load 경로.</summary>
        public const string ResourcePath = "Npc/NpcData";

        [Tooltip("NPC 풀(직업명·대사·서식지 종류). Assets/Scripts/Npc/Data/NpcPool.json")]
        [SerializeField] private TextAsset _pool;
        [Tooltip("대화 패널 선택지(라벨·번영 변화·정차당 1회). Assets/Scripts/Npc/Data/NpcInteraction.json")]
        [SerializeField] private TextAsset _interaction;

        /// <summary>NPC 풀 JSON.</summary>
        public TextAsset Pool => _pool;
        /// <summary>상호작용 선택지 JSON.</summary>
        public TextAsset Interaction => _interaction;
    }
}
