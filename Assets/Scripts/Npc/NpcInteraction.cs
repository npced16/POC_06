using System;
using System.Collections.Generic;

using UnityEngine;

using IronRing.Contracts;

namespace IronRing.Npc
{
    /// <summary>
    /// 스토리 017 대화·상호작용(story-017-npc-homes-dialogue.md, AC "대화 패널"·"대사").
    /// 표시 이름은 NPC 풀의 직업명, 대사는 `_quote` 다음 `_lines`를 순환한다. 선택지는 NpcInteraction.json의 데이터이며 실행하면 다음 대사로 넘어간다.
    /// </summary>
    public sealed class NpcInteraction : INpcInteraction, IDisposable
    {
        private readonly NpcPoolData _pool;
        private readonly Settings _settings;
        private readonly Dictionary<string, int> _lineIndex = new Dictionary<string, int>(StringComparer.Ordinal);

        /// <summary>npcId, optionId. 실행에 성공했을 때.</summary>
        public event Action<string, string> Interacted;

        /// <summary>pool의 대사와 settings의 선택지를 사용하는 상호작용 시스템을 만든다.</summary>
        public NpcInteraction(NpcPoolData pool, Settings settings)
        {
            _pool = pool ?? throw new ArgumentNullException(nameof(pool));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
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

        /// <summary>데이터 순서의 선택지를 모두 활성 상태로 반환한다.</summary>
        public IReadOnlyList<NpcInteractionOption> Options(string npcId)
        {
            var list = new List<NpcInteractionOption>();
            foreach (OptionData o in _settings._options ?? Array.Empty<OptionData>())
            {
                if (o == null || string.IsNullOrEmpty(o._id)) continue;
                list.Add(new NpcInteractionOption { Id = o._id, Label = o._label ?? o._id, Enabled = true });
            }
            return list.AsReadOnly();
        }

        /// <summary>선택지를 실행해 다음 대사를 resultText에 담는다. 성공하면 Interacted.</summary>
        public bool TryInteract(string npcId, string optionId, out string resultText)
        {
            resultText = null;
            if (_pool.Find(npcId) == null) return false;
            OptionData option = Find(optionId);
            if (option == null) return false;

            resultText = NextLine(npcId);
            Interacted?.Invoke(npcId, option._id);
            return true;
        }

        /// <summary>Interacted 구독을 비운다.</summary>
        public void Dispose()
        {
            Interacted = null;
        }

        private OptionData Find(string optionId)
        {
            foreach (OptionData o in _settings._options ?? Array.Empty<OptionData>())
                if (o != null && o._id == optionId) return o;
            return null;
        }

#pragma warning disable 0649
        /// <summary>NpcInteraction.json 형식. 모든 문구는 한국어 데이터.</summary>
        [Serializable]
        public sealed class Settings
        {
            /// <summary>패널 선택지(순서대로 버튼).</summary>
            public OptionData[] _options;

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
        }
#pragma warning restore 0649
    }
}
