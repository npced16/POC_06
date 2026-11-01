using System;
using System.Collections.Generic;
using System.Globalization;

using UnityEngine;

using IronRing.Contracts;

namespace IronRing.Npc
{
    /// <summary>
    /// 스토리 017 대화·상호작용(story-017-npc-homes-dialogue.md, AC "대화 패널"·"격려 결과"·"대사").
    /// 표시 이름은 NPC 풀의 직업명, 대사는 `_quote` 다음 `_lines`를 순환한다. 선택지는 NpcInteraction.json의 데이터다:
    /// 번영 변화(`_prosperityDelta`)가 0이면 대화(다음 대사), 0이 아니면 그 NPC의 거점(집 거점, 없으면 합류 거점)에 번영을 더한다.
    /// `_oncePerDocking` 선택지는 NPC마다 정차당 한 번이며, ITrackRuntime.Arrived(출발 뒤 다음 칸에 닿을 때 포함)에서 초기화된다.
    /// 계약에 출발 이벤트가 없어 Arrived로 초기화한다: 출발한 열차는 다음 노드에 닿는 즉시 초기화되므로 다음 정차에서는 다시 격려할 수 있다.
    /// </summary>
    public sealed class NpcInteraction : INpcInteraction, IDisposable
    {
        private readonly NpcPoolData _pool;
        private readonly Settings _settings;
        private readonly IHubStateSystem _hubs;
        private readonly INpcRecruiter _recruiter;
        private readonly Func<INpcHomes> _homes;
        private readonly ITrackRuntime _track;
        private readonly Dictionary<string, int> _lineIndex = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly HashSet<string> _used = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>npcId, optionId. 실행에 성공했을 때.</summary>
        public event Action<string, string> Interacted;

        /// <summary>homes는 늦게 얻어도 된다(null을 돌려주면 합류 거점을 쓴다). track이 null이면 격려 횟수는 초기화되지 않는다.</summary>
        public NpcInteraction(NpcPoolData pool, Settings settings, IHubStateSystem hubs, INpcRecruiter recruiter, Func<INpcHomes> homes, ITrackRuntime track)
        {
            _pool = pool ?? throw new ArgumentNullException(nameof(pool));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _hubs = hubs ?? throw new ArgumentNullException(nameof(hubs));
            _recruiter = recruiter ?? throw new ArgumentNullException(nameof(recruiter));
            _homes = homes ?? (() => null);
            _track = track;
            if (_track != null) _track.Arrived += OnArrived;
        }

        /// <summary>표시 이름(직업명). 모르는 id면 id 그대로.</summary>
        public string DisplayName(string npcId) => _pool.Find(npcId)?.Name ?? npcId ?? "";

        /// <summary>스토리 023: 출신지 이름(NpcPool.json `_originName`). 모르면 null.</summary>
        public string Origin(string npcId) => _pool.Find(npcId)?.Passenger?.OriginName;

        /// <summary>현재 대사. NextLine을 부르기 전에는 첫 줄(_quote).</summary>
        public string CurrentLine(string npcId)
        {
            IReadOnlyList<string> lines = _pool.Find(npcId)?.Lines;
            if (lines == null || lines.Count == 0) return "";
            _lineIndex.TryGetValue(npcId, out int index);
            return lines[index % lines.Count];
        }

        /// <summary>다음 대사로 넘기고 그 줄을 돌려준다(끝이면 처음으로).</summary>
        public string NextLine(string npcId)
        {
            IReadOnlyList<string> lines = _pool.Find(npcId)?.Lines;
            if (lines == null || lines.Count == 0) return "";
            _lineIndex.TryGetValue(npcId, out int index);
            _lineIndex[npcId] = (index + 1) % lines.Count;
            return lines[_lineIndex[npcId]];
        }

        /// <summary>데이터 순서의 선택지. 정차당 1회 선택지를 이미 썼거나 거점이 없으면 Enabled=false와 이유.</summary>
        public IReadOnlyList<NpcInteractionOption> Options(string npcId)
        {
            var list = new List<NpcInteractionOption>();
            foreach (OptionData o in _settings._options ?? Array.Empty<OptionData>())
            {
                if (o == null || string.IsNullOrEmpty(o._id)) continue;
                string reason = BlockReason(npcId, o);
                list.Add(new NpcInteractionOption { Id = o._id, Label = o._label ?? o._id, Enabled = reason == null, Reason = reason });
            }
            return list.AsReadOnly();
        }

        /// <summary>선택지를 실행한다. 대화는 다음 대사, 번영 선택지는 IHubStateSystem.ApplyDelta. 성공하면 Interacted.</summary>
        public bool TryInteract(string npcId, string optionId, out string resultText)
        {
            resultText = null;
            if (_pool.Find(npcId) == null) return false;
            OptionData option = Find(optionId);
            if (option == null) return false;
            string reason = BlockReason(npcId, option);
            if (reason != null) { resultText = reason; return false; }

            if (Mathf.Approximately(option._prosperityDelta, 0f))
            {
                resultText = NextLine(npcId);
            }
            else
            {
                string hub = HubOf(npcId);
                _hubs.ApplyDelta(hub, option._prosperityDelta, "npc-" + option._id + ":" + npcId);
                if (option._oncePerDocking) _used.Add(Key(npcId, option._id));
                resultText = string.Format(CultureInfo.InvariantCulture, _settings._encourageResultFormat ?? "{0}",
                    option._prosperityDelta.ToString("0.##", CultureInfo.InvariantCulture));
            }
            Interacted?.Invoke(npcId, option._id);
            return true;
        }

        /// <summary>정차 제한을 지운다(통합은 ITrackRuntime.Arrived에서 자동 호출).</summary>
        public void ResetDocking() => _used.Clear();

        /// <summary>트랙 구독을 끊는다.</summary>
        public void Dispose()
        {
            if (_track != null) _track.Arrived -= OnArrived;
            Interacted = null;
        }

        private void OnArrived(ITrackNode node) => ResetDocking();

        private string BlockReason(string npcId, OptionData o)
        {
            if (Mathf.Approximately(o._prosperityDelta, 0f)) return null;
            if (HubOf(npcId) == null) return _settings._noHubReason;
            if (o._oncePerDocking && _used.Contains(Key(npcId, o._id))) return _settings._usedReason;
            return null;
        }

        /// <summary>집 거점, 없으면 합류한 거점, 둘 다 없으면 null.</summary>
        private string HubOf(string npcId)
        {
            if (npcId == null) return null;
            string home = _homes()?.HomeOf(npcId)?.HubId;
            if (home != null) return home;
            foreach (string hub in _hubs.HubIds)
                foreach (string npc in _recruiter.NpcsAt(hub))
                    if (npc == npcId) return hub;
            return null;
        }

        private OptionData Find(string optionId)
        {
            foreach (OptionData o in _settings._options ?? Array.Empty<OptionData>())
                if (o != null && o._id == optionId) return o;
            return null;
        }

        private static string Key(string npcId, string optionId) => npcId + "\n" + optionId;

#pragma warning disable 0649
        /// <summary>NpcInteraction.json 형식. 모든 문구는 한국어 데이터.</summary>
        [Serializable]
        public sealed class Settings
        {
            /// <summary>패널 선택지(순서대로 버튼).</summary>
            public OptionData[] _options;
            /// <summary>번영 선택지 결과 문장. {0}은 번영 변화량.</summary>
            public string _encourageResultFormat = "격려했다. 번영 +{0}";
            /// <summary>정차당 1회 선택지를 이미 썼을 때의 비활성 이유.</summary>
            public string _usedReason = "이번 정차에는 이미 격려했다";
            /// <summary>합류하지 않은 NPC라 거점이 없을 때의 비활성 이유.</summary>
            public string _noHubReason = "거점에 머무는 주민이 아니다";

            /// <summary>JSON을 읽는다. 선택지가 없으면 예외.</summary>
            public static Settings FromJson(string json)
            {
                Settings s = string.IsNullOrWhiteSpace(json) ? null : JsonUtility.FromJson<Settings>(json);
                if (s == null || s._options == null || s._options.Length == 0)
                    throw new ArgumentException("NpcInteraction JSON must contain _options.", nameof(json));
                return s;
            }
        }

        /// <summary>선택지 하나.</summary>
        [Serializable]
        public sealed class OptionData
        {
            public string _id;
            public string _label;
            /// <summary>0이면 대화(다음 대사), 아니면 NPC 거점의 번영 변화량.</summary>
            public float _prosperityDelta;
            /// <summary>true면 NPC마다 정차당 한 번.</summary>
            public bool _oncePerDocking;
        }
#pragma warning restore 0649
    }
}
