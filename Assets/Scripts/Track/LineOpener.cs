using System;
using System.Collections.Generic;

using IronRing.Contracts;

namespace IronRing.Track
{
    /// <summary>
    /// 스토리 048 (design/game-brief.md "일정 이상 진행되면 선로가 열린다"). ILineOpener 구현. 순수 C#.
    /// ring-track.json `_locked` 역을 RingTrack.Unlock으로 연다. 열린 뒤 다음 도착부터 정차·내부·승객이 정상 동작한다.
    /// </summary>
    // 스펙 003: 저장 슬롯 참여 계약 ISaveParticipant 구현 추가(스펙 표에 없던 열린 선로 상태)
    public sealed class LineOpener : ILineOpener, IDisposable, ISaveParticipant
    {
        private readonly RingTrack _track;
        private readonly List<string> _locked = new List<string>();

        /// <inheritdoc />
        public event Action<string> HubOpened;
        /// <inheritdoc />
        public IReadOnlyList<string> LockedHubIds => _locked;

        /// <summary>선로를 주입하고 잠긴 역을 모은다.</summary>
        public LineOpener(RingTrack track)
        {
            _track = track ?? throw new ArgumentNullException(nameof(track));
            foreach (TrackNode node in track.Nodes) if (node.IsStation && node.IsLocked) _locked.Add(node.Id);
            _track.PassedLocked += OnPassed;
        }

        /// <inheritdoc />
        public bool IsOpen(string hubId) => !_locked.Contains(hubId);

        // 스펙 003: Open 메서드에 summary 주석 추가
        /// <summary>
        /// 잠긴 거점 hubId의 선로를 연다. 잠긴 목록에서 빼고 선로를 풀어 준 뒤 HubOpened를 낸다.
        /// 이미 열린 거점이면 아무것도 하지 않는다.
        /// </summary>
        public void Open(string hubId)
        {
            if (!_locked.Remove(hubId)) return;
            _track.Unlock(hubId);
            UnityEngine.Debug.Log($"LINE: opened {hubId}");
            HubOpened?.Invoke(hubId);
        }

        private static void OnPassed(TrackNode node) => UnityEngine.Debug.Log($"LINE: pass {node.Id} locked");

        /// <summary>구독을 푼다.</summary>
        public void Dispose()
        {
            _track.PassedLocked -= OnPassed;
            HubOpened = null;
        }

        // 스펙 003: 저장 슬롯용 아직 잠긴 역 꺼내기·되돌리기 추가
        /// <summary>스펙 003. 슬롯 파일 안의 키.</summary>
        public string SaveKey => "lineOpener";

        /// <summary>스펙 003. 복원 순서. 거점 상태(70)·주민(71)·등급(72)보다 먼저 역을 열어 마을 연결이 붙게 한다.</summary>
        public int Order => 65;

        /// <summary>아직 잠긴 역 _locked(선로 순서)를 JSON으로 돌려준다. 상태를 바꾸지 않는다.</summary>
        public string Capture()
        {
            return UnityEngine.JsonUtility.ToJson(new LineSave { Locked = _locked.ToArray() });
        }

        /// <summary>
        /// Capture가 만든 json에서 잠겨 있지 않은 역을 Open으로 연다. 선로 잠금이 풀리고 HubOpened가 나서
        /// 경제 설치기가 거점 열기·마을 세기·마을 연결을 붙인다. 잠긴 역을 다시 잠그지는 않는다.
        /// </summary>
        public void Restore(string json)
        {
            LineSave state = UnityEngine.JsonUtility.FromJson<LineSave>(json);
            HashSet<string> stillLocked = new HashSet<string>(state.Locked, StringComparer.Ordinal);
            foreach (string id in _locked.ToArray())
            {
                if (!stillLocked.Contains(id)) Open(id);
            }
        }

        /// <summary>스펙 003. 저장 형식: 아직 잠긴 역 id 목록.</summary>
        [Serializable]
        private sealed class LineSave
        {
            public string[] Locked = Array.Empty<string>();
        }
    }
}
