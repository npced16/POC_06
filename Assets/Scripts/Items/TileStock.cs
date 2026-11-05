using System;
using System.Collections.Generic;

using IronRing.Contracts;

namespace IronRing.Items
{
    /// <summary>
    /// 스토리 051(story-051-phase-loop-events.md "타일 재고"). 중간역 사건에서 얻은 타일 묶음 재고. 품목별 수만 갖는다(상한 없음).
    /// 배치 결제는 ItemPlacement.Stock이 먼저 쓴다(재고 1 = 무료 배치).
    /// </summary>
    // 스펙 003: 저장 슬롯 참여 계약 ISaveParticipant 구현 추가
    public sealed class TileStock : ITileStock, ISaveParticipant
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

        // 스펙 003: 저장 슬롯용 퀵슬롯 재고 꺼내기·되돌리기 추가
        /// <summary>스펙 003. 슬롯 파일 안의 키.</summary>
        public string SaveKey => "tileStock";

        /// <summary>스펙 003. 복원 순서.</summary>
        public int Order => 22;

        /// <summary>처음 얻은 순서 _order와 품목별 수 _counts를 JSON으로 돌려준다. 상태를 바꾸지 않는다.</summary>
        public string Capture()
        {
            StockSave state = new StockSave { Ids = _order.ToArray(), Counts = new int[_order.Count] };
            for (int i = 0; i < _order.Count; i++) state.Counts[i] = _counts[_order[i]];
            return UnityEngine.JsonUtility.ToJson(state);
        }

        /// <summary>
        /// Capture가 만든 json으로 _order와 _counts를 바꾼다. 수가 바뀐 품목(사라진 품목은 0)마다 Changed를 낸다.
        /// </summary>
        public void Restore(string json)
        {
            StockSave state = UnityEngine.JsonUtility.FromJson<StockSave>(json);
            Dictionary<string, int> before = new Dictionary<string, int>(_counts, StringComparer.Ordinal);
            _counts.Clear();
            _order.Clear();
            for (int i = 0; i < state.Ids.Length && i < state.Counts.Length; i++)
            {
                if (string.IsNullOrEmpty(state.Ids[i]) || _counts.ContainsKey(state.Ids[i])) continue;
                _order.Add(state.Ids[i]);
                _counts[state.Ids[i]] = state.Counts[i];
            }
            foreach (KeyValuePair<string, int> entry in _counts)
            {
                if (!before.TryGetValue(entry.Key, out int old) || old != entry.Value) Changed?.Invoke(entry.Key, entry.Value);
            }
            foreach (KeyValuePair<string, int> entry in before)
            {
                if (!_counts.ContainsKey(entry.Key) && entry.Value != 0) Changed?.Invoke(entry.Key, 0);
            }
        }

        /// <summary>스펙 003. 저장 형식: 처음 얻은 순서의 품목 id와 수의 짝(같은 순번).</summary>
        [Serializable]
        private sealed class StockSave
        {
            public string[] Ids = Array.Empty<string>();
            public int[] Counts = Array.Empty<int>();
        }
    }
}
