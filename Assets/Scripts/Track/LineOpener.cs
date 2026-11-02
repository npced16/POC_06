using System;
using System.Collections.Generic;

using IronRing.Contracts;

namespace IronRing.Track
{
    /// <summary>
    /// 스토리 048 (design/game-brief.md "일정 이상 진행되면 선로가 열린다"). ILineOpener 구현. 순수 C#.
    /// ring-track.json `_locked` 역을 RingTrack.Unlock으로 연다. 열린 뒤 다음 도착부터 정차·내부·승객이 정상 동작한다.
    /// </summary>
    public sealed class LineOpener : ILineOpener, IDisposable
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

        /// <inheritdoc />
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
    }
}
