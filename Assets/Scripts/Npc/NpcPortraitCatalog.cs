using System;
using System.Collections.Generic;

using UnityEngine;

namespace IronRing.Npc
{
    /// <summary>
    /// 스토리 029. NPC id → 상반신 초상 스프라이트 목록. 생성기(SpriteGenPortraits)가 채우고, 대화 화면이
    /// Resources.Load&lt;NpcPortraitCatalog&gt;("Npc/NpcPortraits")로 읽는다. 없는 id는 대체 초상을 돌려준다.
    /// </summary>
    [CreateAssetMenu(menuName = "IronRing/NPC Portrait Catalog")]
    public sealed class NpcPortraitCatalog : ScriptableObject
    {
        /// <summary>id와 초상 한 쌍.</summary>
        [Serializable]
        public sealed class Entry
        {
            [SerializeField] private string _id;
            [SerializeField] private Sprite _sprite;
            public string Id => _id;
            public Sprite Sprite => _sprite;
        }

        [SerializeField] private Entry[] _entries = Array.Empty<Entry>();
        [SerializeField] private Sprite _fallback;

        private Dictionary<string, Sprite> _lookup;

        /// <summary>등록된 초상 수.</summary>
        public int Count => _entries?.Length ?? 0;

        /// <summary>id의 초상, 없으면 대체 초상(그것도 없으면 null).</summary>
        public Sprite Get(string id)
        {
            if (_lookup == null || _lookup.Count != Count)
            {
                _lookup = new Dictionary<string, Sprite>();
                foreach (Entry entry in _entries ?? Array.Empty<Entry>())
                    if (entry != null && !string.IsNullOrEmpty(entry.Id) && entry.Sprite != null) _lookup[entry.Id] = entry.Sprite;
            }
            return id != null && _lookup.TryGetValue(id, out Sprite sprite) ? sprite : _fallback;
        }
    }
}
