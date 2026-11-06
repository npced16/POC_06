using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

using IronRing.Contracts;

namespace IronRing.Track
{
    /// <summary>Immutable node identity and station classification supplied by track data.</summary>
    public sealed class TrackNode : ITrackNode
    {
        /// <summary>Stable identity supplied by the external node data.</summary>
        public string Id { get; }
        /// <summary>Whether arrival requires an explicit station departure.</summary>
        public bool IsStation { get; }
        /// <summary>Zero-based position assigned when the immutable ring snapshot is constructed.</summary>
        public int Index { get; }
        /// <summary>Shared classification derived from the authored station flag.</summary>
        public TrackNodeKind Kind => IsStation ? TrackNodeKind.Hub : TrackNodeKind.Waypoint;
        /// <summary>Stable station identity, or null for a waypoint.</summary>
        public string HubId => IsStation ? Id : null;
        /// <summary>스토리 048. 잠긴 역이면 true(RingTrack.Unlock으로만 풀린다). 열차는 멈추지 않고 지나간다.</summary>
        public bool IsLocked { get; internal set; }

        /// <summary>Validates id and stores station classification and ordered index without allowing subsequent topology edits.</summary>
        public TrackNode(string id, bool isStation, int index = 0, bool locked = false)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Node ID must not be blank.", nameof(id));
            Id = id;
            IsStation = isStation;
            Index = index;
            IsLocked = locked;
        }
    }

    /// <summary>Pure C# one-edge movement with immutable circular topology and explicit station departure.</summary>
    // 스펙 003: 저장 슬롯 참여 계약 ISaveParticipant 구현 추가
    public sealed class RingTrack : ITrackRuntime, ISaveParticipant
    {
        private readonly ReadOnlyCollection<TrackNode> _nodes;
        private readonly Dictionary<string, int> _holds = new Dictionary<string, int>(StringComparer.Ordinal);
        /// <summary>Read-only ordered topology, including all stations and waypoints.</summary>
        public IReadOnlyList<TrackNode> Nodes => _nodes;
        /// <summary>Immutable topology exposed through the cross-system contract.</summary>
        IReadOnlyList<ITrackNode> ITrackRuntime.Nodes => _nodes;
        /// <summary>Current node exposed through the cross-system contract.</summary>
        ITrackNode ITrackRuntime.CurrentNode => CurrentNode;
        /// <summary>Number of nodes classified as stations in the supplied configuration.</summary>
        public int StationCount { get; }
        /// <summary>Index of the last reached node, retained throughout transit.</summary>
        public int CurrentIndex { get; private set; }
        /// <summary>Index of the next node, wrapping to zero at the end of the ring.</summary>
        public int NextIndex => (CurrentIndex + 1) % _nodes.Count;
        /// <summary>Last reached node available to HUD and gameplay subscribers.</summary>
        public TrackNode CurrentNode => _nodes[CurrentIndex];
        /// <summary>Number of completed circuits since starting at node zero.</summary>
        public int LapCount { get; private set; }
        /// <summary>Completed circuit count exposed through the shared contract.</summary>
        public int Lap => LapCount;
        /// <summary>Whether departure has occurred and arrival is pending.</summary>
        public bool IsMoving { get; private set; }
        /// <summary>Whether the train is currently stopped at a station.</summary>
        public bool IsStopped => !IsMoving && CurrentNode.IsStation;
        /// <summary>Whether any outstanding encounter hold prevents departure or progress along an edge.</summary>
        public bool HasActiveHolds => _holds.Count > 0;
        /// <summary>Whether the train is docked at a station or held by a gameplay system.</summary>
        public bool IsHeld => IsStopped || HasActiveHolds;
        /// <summary>Emitted once after the current node and lap count have been committed on arrival.</summary>
        public event Action<TrackNode> PositionChanged;
        /// <summary>Emitted on waypoint arrival; subscribers own the actual event content.</summary>
        public event Action<TrackNode> WaypointReached;
        /// <summary>Shared arrival notification emitted after current node and lap state are committed.</summary>
        public event Action<ITrackNode> Arrived;
        /// <summary>Emitted exactly once for each completed circuit, carrying the new lap count.</summary>
        public event Action<int> LapCompleted;
        /// <summary>스토리 048. 잠긴 노드를 멈추지 않고 지나갈 때(Arrived·WaypointReached 대신).</summary>
        public event Action<TrackNode> PassedLocked;
        /// <summary>스토리 048. 잠긴 노드가 풀렸을 때 한 번.</summary>
        public event Action<TrackNode> NodeUnlocked;

        /// <summary>Copies ordered nodes, rejects empty or duplicate topology and derives station count from the input.</summary>
        public RingTrack(IEnumerable<TrackNode> nodes)
        {
            if (nodes == null) throw new ArgumentNullException(nameof(nodes));
            var copy = new List<TrackNode>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (TrackNode node in nodes)
            {
                if (node == null || !ids.Add(node.Id)) throw new ArgumentException("Nodes must have unique non-null identities.", nameof(nodes));
                copy.Add(new TrackNode(node.Id, node.IsStation, copy.Count, node.IsLocked));
                if (node.IsStation) StationCount++;
            }
            if (copy.Count == 0) throw new ArgumentException("A ring requires at least one node.", nameof(nodes));
            if (copy[0].IsLocked) throw new ArgumentException("The first node (lap origin) cannot be locked.", nameof(nodes));
            _nodes = copy.AsReadOnly();
        }

        /// <summary>Starts travel to the next node; returns false if already travelling and otherwise changes IsMoving.</summary>
        public bool TryDepart()
        {
            if (IsMoving || HasActiveHolds) return false;
            IsMoving = true;
            return true;
        }

        /// <summary>Completes the active edge, advances current node and lap count, then emits arrival notifications.</summary>
        public void Arrive()
        {
            if (!IsMoving) throw new InvalidOperationException("Departure is required before arrival.");
            if (HasActiveHolds) throw new InvalidOperationException("All holds must be released before arrival.");
            CurrentIndex = NextIndex;
            if (CurrentIndex == 0) LapCount++;
            TrackNode arrived = CurrentNode;
            if (arrived.IsLocked)
            {
                // 스토리 048: 잠긴 노드는 통과만 한다. 위치는 넘기고(지도 보간이 다음 칸으로 이어진다) 계속 달린다. 첫 노드는 잠길 수 없다.
                PositionChanged?.Invoke(arrived);
                PassedLocked?.Invoke(arrived);
                return;
            }
            IsMoving = false;
            if (CurrentIndex == 0) LapCompleted?.Invoke(LapCount);
            PositionChanged?.Invoke(arrived);
            Arrived?.Invoke(arrived);
            if (!arrived.IsStation) WaypointReached?.Invoke(arrived);
        }

        /// <summary>스토리 048. nodeId의 잠금을 푼다. 잠겨 있던 노드였으면 NodeUnlocked 후 true.</summary>
        public bool Unlock(string nodeId)
        {
            foreach (TrackNode node in _nodes)
            {
                if (node.Id != nodeId || !node.IsLocked) continue;
                node.IsLocked = false;
                NodeUnlocked?.Invoke(node);
                return true;
            }
            return false;
        }

        /// <summary>Requests departure through the shared runtime API; has no effect during movement or an active hold.</summary>
        public void Depart()
        {
            TryDepart();
        }

        /// <summary>Adds one hold for the supplied nonblank reason; repeated and independent reasons must all be released.</summary>
        public void RequestHold(string reason)
        {
            if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("A hold requires a reason.", nameof(reason));
            _holds.TryGetValue(reason, out int count);
            _holds[reason] = count + 1;
        }

        /// <summary>Releases one hold for the supplied reason, leaving other holds intact and ignoring unmatched releases.</summary>
        public void ReleaseHold(string reason)
        {
            if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("A hold requires a reason.", nameof(reason));
            if (!_holds.TryGetValue(reason, out int count)) return;
            if (count == 1) _holds.Remove(reason);
            else _holds[reason] = count - 1;
        }

        // 스펙 003: 저장 슬롯용 바퀴 수 꺼내기·되돌리기 추가(복원 때 열차를 거점 정차 위치에 둔다)
        /// <summary>스펙 003. 슬롯 파일 안의 키.</summary>
        public string SaveKey => "ringTrack";

        /// <summary>스펙 003. 복원 순서.</summary>
        public int Order => 90;

        /// <summary>완료 바퀴 수 LapCount를 JSON으로 돌려준다. 저장은 건설 페이즈(거점 정차)에서만 하므로 위치는 저장하지 않는다.</summary>
        public string Capture()
        {
            return UnityEngine.JsonUtility.ToJson(new TrackSave { LapCount = LapCount });
        }

        /// <summary>
        /// Capture가 만든 json으로 LapCount를 두고 열차를 첫 노드(거점)에 멈춘 상태로 둔 뒤 PositionChanged를 낸다.
        /// 도착 처리(조사 횟수 초기화·정산)가 다시 일어나지 않도록 Arrived·LapCompleted는 내지 않는다.
        /// </summary>
        public void Restore(string json)
        {
            TrackSave state = UnityEngine.JsonUtility.FromJson<TrackSave>(json);
            LapCount = Math.Max(0, state.LapCount);
            CurrentIndex = 0;
            IsMoving = false;
            PositionChanged?.Invoke(CurrentNode);
        }

        /// <summary>스펙 003. 저장 형식: 완료 바퀴 수.</summary>
        [Serializable]
        private sealed class TrackSave
        {
            public int LapCount;
        }
    }
}
