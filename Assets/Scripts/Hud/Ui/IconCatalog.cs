using System;

using UnityEngine;

namespace IronRing.Hud.Ui
{
    [CreateAssetMenu(menuName = "IronRing/Icon Catalog")]
    public sealed class IconCatalog : ScriptableObject
    {
        [Header("Icons")]
        [SerializeField] private Entry[] _entries = Array.Empty<Entry>();

        [Serializable]
        private sealed class Entry
        {
            [SerializeField] private string _id;
            [SerializeField] private Sprite _sprite;
            public string Id => _id;
            public Sprite Sprite => _sprite;
        }

        /// <summary>Returns the registered sprite for id, or null when no entry matches.</summary>
        public Sprite Get(string id)
        {
            foreach (Entry entry in _entries) if (entry.Id == id) return entry.Sprite;
            return null;
        }
    }
}
