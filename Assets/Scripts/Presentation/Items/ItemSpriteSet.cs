using System;
using System.Collections.Generic;

using UnityEngine;

namespace IronRing.Presentation.Items
{
    /// <summary>
    /// 스토리 013. 아이템 id → 32픽셀 스프라이트 표. SpriteGenItems.Generate가 Resources/Items/ItemSprites.asset으로 만든다.
    /// 씬 참조 없이 Resources로 읽어 칸 뷰와 인벤토리 HUD가 같이 쓴다.
    /// </summary>
    [CreateAssetMenu(menuName = "IronRing/Item Sprite Set")]
    public sealed class ItemSpriteSet : ScriptableObject
    {
        [Serializable]
        private sealed class Entry
        {
            [SerializeField] public string _id;
            [SerializeField] public Sprite _sprite;
        }

        [SerializeField] private Entry[] _entries = Array.Empty<Entry>();
        private Dictionary<string, Sprite> _lookup;

        /// <summary>id의 스프라이트, 없으면 null.</summary>
        public Sprite Get(string itemId)
        {
            if (itemId == null) return null;
            if (_lookup == null)
            {
                _lookup = new Dictionary<string, Sprite>(StringComparer.Ordinal);
                foreach (Entry e in _entries) if (e != null && !string.IsNullOrEmpty(e._id)) _lookup[e._id] = e._sprite;
            }
            return _lookup.TryGetValue(itemId, out Sprite s) ? s : null;
        }

        /// <summary>편집기 생성기가 표를 통째로 바꾼다.</summary>
        public void SetEntries(IReadOnlyList<KeyValuePair<string, Sprite>> entries)
        {
            var list = new List<Entry>();
            foreach (var e in entries) list.Add(new Entry { _id = e.Key, _sprite = e.Value });
            _entries = list.ToArray();
            _lookup = null;
        }

        /// <summary>Resources/Items/ItemSprites를 읽는다. 아직 생성 전이면 null.</summary>
        public static ItemSpriteSet Load() => UnityEngine.Resources.Load<ItemSpriteSet>("Items/ItemSprites");
    }
}
