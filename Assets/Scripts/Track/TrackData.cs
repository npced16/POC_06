using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

using UnityEngine;

namespace IronRing.Track
{
    /// <summary>Authored track configuration loaded from JSON, with all layout and timing values editable.</summary>
    [Serializable]
    public sealed class TrackData
    {
        [Header("Movement")]
        [SerializeField] private float _edgeSeconds;
        [SerializeField] private bool _autoDepart;
        [SerializeField] private float _stationWaitSeconds;
        [Header("Presentation")]
        [SerializeField] private Vector2 _referenceSize;
        [SerializeField] private float _stationSize;
        [SerializeField] private float _waypointSize;
        [SerializeField] private Vector2 _trainSize;
        [SerializeField] private float _railWidth;
        [SerializeField] private int _fontSize;
        [SerializeField] private Color _background;
        [SerializeField] private Color _rail;
        [SerializeField] private Color _station;
        [SerializeField] private Color _waypoint;
        [SerializeField] private Color _train;
        [Header("Nodes")]
        [SerializeField] private NodeData[] _nodes;
        private ReadOnlyCollection<NodeData> _readOnlyNodes;

        /// <summary>Authored duration for one edge of travel.</summary>
        public float EdgeSeconds => _edgeSeconds;
        /// <summary>Explicit configuration switch for automatic station departures.</summary>
        public bool AutoDepart => _autoDepart;
        /// <summary>Station dwell duration when automatic demonstration is enabled.</summary>
        public float StationWaitSeconds => _stationWaitSeconds;
        /// <summary>Authored coordinate space scaled to the current screen.</summary>
        public Vector2 ReferenceSize => _referenceSize;
        /// <summary>Station marker dimensions in reference coordinates.</summary>
        public float StationSize => _stationSize;
        /// <summary>Waypoint marker dimensions in reference coordinates.</summary>
        public float WaypointSize => _waypointSize;
        /// <summary>Train body dimensions in reference coordinates.</summary>
        public Vector2 TrainSize => _trainSize;
        /// <summary>Rail stroke width in reference coordinates.</summary>
        public float RailWidth => _railWidth;
        /// <summary>Label font size in reference coordinates.</summary>
        public int FontSize => _fontSize;
        /// <summary>Authored background colour.</summary>
        public Color Background => _background;
        /// <summary>Authored rail colour.</summary>
        public Color Rail => _rail;
        /// <summary>Authored station and text colour.</summary>
        public Color Station => _station;
        /// <summary>Authored waypoint colour.</summary>
        public Color Waypoint => _waypoint;
        /// <summary>Authored train body colour.</summary>
        public Color Train => _train;
        /// <summary>Read-only node layout preserving external JSON order.</summary>
        public IReadOnlyList<NodeData> Nodes => _readOnlyNodes;

        /// <summary>Parses supplied JSON and validates movement and layout values, returning usable authored configuration.</summary>
        public static TrackData Parse(string json)
        {
            TrackData data = JsonUtility.FromJson<TrackData>(json);
            if (data == null || data._nodes == null || !IsPositive(data._edgeSeconds) ||
                !IsPositive(data._referenceSize.x) || !IsPositive(data._referenceSize.y) ||
                !IsPositive(data._stationSize) || !IsPositive(data._waypointSize) ||
                !IsPositive(data._trainSize.x) || !IsPositive(data._trainSize.y) ||
                !IsPositive(data._railWidth) || data._fontSize <= 0 ||
                float.IsNaN(data._stationWaitSeconds) || float.IsInfinity(data._stationWaitSeconds) || data._stationWaitSeconds < 0)
                throw new ArgumentException("Invalid track timing or layout configuration.", nameof(json));
            data._readOnlyNodes = Array.AsReadOnly(data._nodes);
            data.CreateTrack();
            return data;
        }

        /// <summary>Validates a numeric input and returns whether it is finite and strictly positive.</summary>
        private static bool IsPositive(float value)
        {
            return value > 0 && !float.IsInfinity(value);
        }

        /// <summary>Converts the authored ordered node list into an independent immutable movement model.</summary>
        public RingTrack CreateTrack()
        {
            var nodes = new List<TrackNode>();
            foreach (NodeData node in _nodes)
            {
                if (node == null || float.IsNaN(node.Position.x) || float.IsInfinity(node.Position.x) ||
                    float.IsNaN(node.Position.y) || float.IsInfinity(node.Position.y))
                    throw new ArgumentException("Track nodes require finite positions.");
                nodes.Add(new TrackNode(node.Id, node.IsStation));
            }
            return new RingTrack(nodes);
        }
    }

    /// <summary>Serialized node identity, classification and authored screen position.</summary>
    [Serializable]
    public sealed class NodeData
    {
        [Header("Node")]
        [SerializeField] private string _id;
        [SerializeField] private bool _isStation;
        [SerializeField] private Vector2 _position;
        /// <summary>Stable identity loaded from the external node definition.</summary>
        public string Id => _id;
        /// <summary>Whether this node is a station requiring an explicit departure.</summary>
        public bool IsStation => _isStation;
        /// <summary>Authored marker position in reference screen coordinates.</summary>
        public Vector2 Position => _position;
    }
}
