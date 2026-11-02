using System;
using System.Collections.Generic;

using UnityEngine;

namespace IronRing.Track
{
    /// <summary>
    /// 스토리 051. 중간역 사건 데이터(Resources/Track/RestEvents.json). 문자열 필드는 ko.json 키다.
    /// 효과 _type: tiles | passenger | money | prosperity | delay | nothing.
    /// </summary>
    [Serializable]
    public sealed class RestEventData
    {
        public const string TILES = "tiles";
        public const string PASSENGER = "passenger";
        public const string MONEY = "money";
        public const string PROSPERITY = "prosperity";
        public const string DELAY = "delay";
        public const string NOTHING = "nothing";

        public int _seed = 5101;
        public float _autoChooseSeconds;
        public float _autoContinueSeconds = 4f;
        public bool _disableAutoBoarding = true;
        public string[] _demoQueue;
        public string _demoBuild = "boiler_room";
        public string _demoPlaceItem = "tile_coal";
        public float _demoChooseSeconds = 1.2f;
        public float _demoContinueSeconds = 1.5f;
        public RestEvent[] _events;

        /// <summary>id로 사건을 찾는다(없으면 null).</summary>
        public RestEvent Find(string id)
        {
            if (_events == null || id == null) return null;
            foreach (RestEvent e in _events) if (e != null && e._id == id) return e;
            return null;
        }

        /// <summary>JSON을 읽고 검증한다. 잘못되면 ArgumentException.</summary>
        public static RestEventData Parse(string json)
        {
            RestEventData data = JsonUtility.FromJson<RestEventData>(json);
            if (data == null || data._events == null || data._events.Length == 0) throw new ArgumentException("RestEvents: no events.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (RestEvent e in data._events)
            {
                if (e == null || string.IsNullOrWhiteSpace(e._id) || !ids.Add(e._id)) throw new ArgumentException("RestEvents: bad or duplicate id.");
                if (e._choices == null || e._choices.Length < 1 || e._choices.Length > 3) throw new ArgumentException("RestEvents: 1~3 choices required: " + e._id);
                foreach (RestChoice c in e._choices)
                {
                    if (c == null) throw new ArgumentException("RestEvents: null choice in " + e._id);
                    if (c._effects == null) continue;
                    foreach (RestEffect f in c._effects)
                        if (f == null || !IsKnown(f._type)) throw new ArgumentException("RestEvents: unknown effect in " + e._id + ": " + f?._type);
                }
            }
            return data;
        }

        private static bool IsKnown(string type) =>
            type == TILES || type == PASSENGER || type == MONEY || type == PROSPERITY || type == DELAY || type == NOTHING;
    }

    /// <summary>스토리 051. 사건 하나.</summary>
    [Serializable]
    public sealed class RestEvent
    {
        public string _id;
        public string _title;
        public string _text;
        public int _weight = 1;
        public int _minLap;
        public RestChoice[] _choices;
    }

    /// <summary>스토리 051. 선택지 하나. _resultFail은 확률 효과(_chance)가 실패했을 때의 결과 문구.</summary>
    [Serializable]
    public sealed class RestChoice
    {
        public string _label;
        public string _result;
        public string _resultFail;
        public RestEffect[] _effects;
    }

    /// <summary>
    /// 스토리 051. 효과 하나. tiles: _items(id·count, 잠긴 타일은 열린 타일로 대체, _lockedMoney&gt;0이면 대신 그만큼 마르크),
    /// _randomUnlocked(열린 타일 무작위 n), _loseRandom(재고에서 무작위 n개 잃음). passenger: _npc(빈 값 = 무작위), _count.
    /// money: _amount(±), _onReturn이면 다음 거점 도착 때. prosperity: _amount(±, 거점). delay: _seconds.
    /// _chance(0..1, 0·1 이상 = 늘): 같은 선택지의 확률 효과는 한 번 굴려 함께 성공·실패한다.
    /// </summary>
    [Serializable]
    public sealed class RestEffect
    {
        public string _type;
        public RestTileCount[] _items;
        public int _randomUnlocked;
        public int _loseRandom;
        public int _lockedMoney;
        public string _npc;
        public int _count;
        public int _amount;
        public float _seconds;
        public bool _onReturn;
        public float _chance;
    }

    /// <summary>스토리 051. 타일 id와 수.</summary>
    [Serializable]
    public sealed class RestTileCount
    {
        public string _id;
        public int _count = 1;
    }
}
