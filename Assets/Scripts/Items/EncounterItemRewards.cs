using System;
using System.Collections.Generic;

using UnityEngine;

using IronRing.Contracts;

namespace IronRing.Items
{
    /// <summary>
    /// 스토리 013. 경유지 이벤트·전투 승리의 아이템 보상. 데이터는 005의 Story005Encounters.json에 붙인 선택 필드
    /// (_rewardItem/_rewardItemAmount, _events[]._item/_itemAmount)이며 005 코드는 바꾸지 않는다.
    /// </summary>
    public sealed class EncounterItemRewards
    {
        private readonly IItemInventory _items;
        private readonly Action<string, int> _grantItem;
        private readonly string _victoryItem;
        private readonly int _victoryAmount;
        private readonly Dictionary<string, KeyValuePair<string, int>> _eventItems;

        /// <summary>승리 보상과 이벤트 제목별 보상을 주입받는다. 비어 있으면 아무것도 주지 않는다.</summary>
        public EncounterItemRewards(IItemInventory items, string victoryItem, int victoryAmount,
            IReadOnlyDictionary<string, KeyValuePair<string, int>> eventItems, Action<string, int> grantItem = null)
        {
            _items = items ?? throw new ArgumentNullException(nameof(items));
            _grantItem = grantItem ?? new Action<string, int>(_items.Add);
            _victoryItem = victoryItem;
            _victoryAmount = Math.Max(0, victoryAmount);
            _eventItems = new Dictionary<string, KeyValuePair<string, int>>(StringComparer.Ordinal);
            if (eventItems != null) foreach (var e in eventItems) _eventItems[e.Key] = e.Value;
        }

        /// <summary>조우 결과와 이벤트 제목에 해당하는 아이템을 지급하고 지급한 id를 반환한다. 보상이 없으면 null을 반환한다.</summary>
        public string Grant(EncounterOutcome outcome, string eventTitle)
        {
            if (outcome == EncounterOutcome.EventResolved && eventTitle != null
                && _eventItems.TryGetValue(eventTitle, out var reward) && !string.IsNullOrEmpty(reward.Key) && reward.Value > 0)
            {
                _grantItem(reward.Key, reward.Value);
                return reward.Key;
            }
            return null;
        }

        /// <summary>json에서 아이템 보상을 읽어 items와 선택적 grantItem 지급 경로를 사용하는 보상 객체를 반환한다.</summary>
        public static EncounterItemRewards Parse(string json, IItemInventory items, Action<string, int> grantItem = null)
        {
            var data = string.IsNullOrWhiteSpace(json) ? null : JsonUtility.FromJson<RewardData>(json);
            var events = new Dictionary<string, KeyValuePair<string, int>>(StringComparer.Ordinal);
            if (data?._events != null)
                foreach (EventData e in data._events)
                    if (e != null && !string.IsNullOrEmpty(e._title) && !string.IsNullOrEmpty(e._item))
                        events[e._title] = new KeyValuePair<string, int>(e._item, e._itemAmount);
            return new EncounterItemRewards(items, data?._rewardItem, data?._rewardItemAmount ?? 0, events, grantItem);
        }

#pragma warning disable 0649
        [Serializable] private sealed class RewardData { public string _rewardItem; public int _rewardItemAmount; public EventData[] _events; }
        [Serializable] private sealed class EventData { public string _title; public string _item; public int _itemAmount; }
#pragma warning restore 0649
    }
}
