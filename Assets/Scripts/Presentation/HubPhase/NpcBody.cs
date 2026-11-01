using UnityEngine;

namespace IronRing.Presentation.HubPhase
{
    /// <summary>
    /// Story 017. Identity of one spawned interior NPC sprite (the HubPhaseNpc prefab carries the trigger BoxCollider2D).
    /// InteriorNpcWalker adds it at spawn so a Physics2D ray can map a click back to (hubId, npcId).
    /// </summary>
    public sealed class NpcBody : MonoBehaviour
    {
        /// <summary>Hub whose interior shows this NPC.</summary>
        public string HubId { get; private set; }
        /// <summary>Joined NPC id.</summary>
        public string NpcId { get; private set; }

        /// <summary>Sets the identity once after spawning.</summary>
        public void Bind(string hubId, string npcId)
        {
            HubId = hubId;
            NpcId = npcId;
        }
    }
}
