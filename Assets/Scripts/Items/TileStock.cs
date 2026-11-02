using System;
using System.Collections.Generic;

using IronRing.Contracts;

namespace IronRing.Items
{
    /// <summary>
    /// 스토리 051(story-051-phase-loop-events.md "타일 재고"). 중간역 사건에서 얻은 타일 묶음 재고. 품목별 수만 갖는다(상한 없음).
    /// 배치 결제는 ItemPlacement.Stock이 먼저 쓴다(재고 1 = 무료 배치).
    /// </summary>
    public sealed class TileStock : ITileStock
    {
        private readonly Dictionary<string, int> _counts = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly List<string> _order = new List<string>();

        /// <summary>itemId, 새 수.</summary>
        public event Action<string, int> Changed;

        /// <summary>재고 수(없으면 0).</summary>
        public int CountOf(string itemId) => itemId != null && _counts.TryGetValue(itemId, out int n) ? n : 0;

        /// <summary>count(&gt;0)만큼 더한다.</summary>
        public void Add(string itemId, int count)
        {
            if (string.IsNullOrEmpty(itemId) || count <= 0) return;
            if (!_counts.ContainsKey(itemId)) _order.Add(itemId);
            int next = CountOf(itemId) + count;
            _counts[itemId] = next;
            Changed?.Invoke(itemId, next);
        }

        /// <summary>count만큼 있으면 빼고 true.</summary>
        public bool TryTake(string itemId, int count)
        {
            if (string.IsNullOrEmpty(itemId) || count <= 0) return false;
            int have = CountOf(itemId);
            if (have < count) return false;
            _counts[itemId] = have - count;
            Changed?.Invoke(itemId, have - count);
            return true;
        }

        /// <summary>재고가 있는 품목(처음 얻은 순서)과 수.</summary>
        public IReadOnlyDictionary<string, int> Snapshot()
        {
            var copy = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (string id in _order) if (_counts[id] > 0) copy[id] = _counts[id];
            return copy;
        }

        /// <summary>재고가 있는 품목 id(처음 얻은 순서).</summary>
        public IReadOnlyList<string> StockedIds()
        {
            var ids = new List<string>();
            foreach (string id in _order) if (_counts[id] > 0) ids.Add(id);
            return ids;
        }
    }
}
