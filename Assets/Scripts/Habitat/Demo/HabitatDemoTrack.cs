using System;
using System.Collections.Generic;

using IronRing.Contracts;

namespace IronRing.Habitat.Demo
{
    /// <summary>Local demo fake for the track contract; does not depend on the real track implementation.</summary>
    public sealed class HabitatDemoTrack : ITrackRuntime
    {
        private readonly List<ITrackNode> _nodes = new List<ITrackNode>();
        private readonly HashSet<string> _holds = new HashSet<string>(StringComparer.Ordinal);
        private bool _docked;

        /// <summary>Configured fake hub nodes in order.</summary>
        public IReadOnlyList<ITrackNode> Nodes => _nodes.AsReadOnly();
        /// <summary>The selected fake node.</summary>
        public ITrackNode CurrentNode { get; private set; }
        /// <summary>The stationary demo does not advance laps.</summary>
        public int Lap => 0;
        /// <summary>Whether the demo train is docked or explicitly held.</summary>
        public bool IsHeld => _docked || _holds.Count > 0;

        /// <summary>Raised when the demo explicitly selects a hub.</summary>
        public event Action<ITrackNode> Arrived;
        /// <summary>Unused contract event because the demo does not simulate laps.</summary>
        public event Action<int> LapCompleted { add { } remove { } }

        /// <summary>Creates fake hub nodes using hubIds in order and docks at the first node.</summary>
        public HabitatDemoTrack(IEnumerable<string> hubIds)
        {
            foreach (string id in hubIds) _nodes.Add(new Node(id, _nodes.Count));
            if (_nodes.Count == 0) throw new ArgumentException("Demo requires a hub.", nameof(hubIds));
            CurrentNode = _nodes[0];
            _docked = true;
        }

        /// <summary>Docks at hubId and emits Arrived; throws if the id is not configured.</summary>
        public void DockAt(string hubId)
        {
            foreach (var node in _nodes)
            {
                if (node.HubId != hubId) continue;
                CurrentNode = node;
                _docked = true;
                Arrived?.Invoke(node);
                return;
            }
            throw new ArgumentException("Unknown demo hub.", nameof(hubId));
        }

        /// <summary>Leaves the current dock when no explicit hold remains, changing IsHeld to false.</summary>
        public void Depart()
        {
            if (_holds.Count == 0) _docked = false;
        }

        /// <summary>Adds reason to active holds, preventing departure until released.</summary>
        public void RequestHold(string reason)
        {
            _holds.Add(reason);
        }

        /// <summary>Removes reason from active holds, allowing departure when no hold remains.</summary>
        public void ReleaseHold(string reason)
        {
            _holds.Remove(reason);
        }

        private sealed class Node : ITrackNode
        {
            public string Id => HubId;
            public int Index { get; }
            public TrackNodeKind Kind => TrackNodeKind.Hub;
            public string HubId { get; }

            /// <summary>Stores configured id and index for a fake hub node.</summary>
            public Node(string id, int index)
            {
                HubId = id;
                Index = index;
            }
        }
    }
}
