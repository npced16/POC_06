using System;
using System.Collections.Generic;

using UnityEngine;

using NUnit.Framework;

using IronRing.Contracts;

namespace IronRing.Track.Tests
{
    public sealed class RingTrackTests
    {
        /// <summary>Creates a two-node fixture from identifiers and kinds and returns an isolated track.</summary>
        private static RingTrack CreateTrack()
        {
            return new RingTrack(new[] { new TrackNode("home", true), new TrackNode("road", false) });
        }

        /// <summary>Checks one-edge movement and wrap using a fixture, including completed lap state.</summary>
        [Test]
        public void Movement_AdvancesOneNodeAndCountsCompletedLaps()
        {
            var track = CreateTrack();
            Assert.That(track.TryDepart(), Is.True);
            Assert.That(track.TryDepart(), Is.False);
            Assert.That(track.CurrentNode.Id, Is.EqualTo("home"));
            track.Arrive();
            Assert.That(track.CurrentNode.Id, Is.EqualTo("road"));
            Assert.That(track.LapCount, Is.Zero);
            track.TryDepart();
            track.Arrive();
            Assert.That(track.CurrentNode.Id, Is.EqualTo("home"));
            Assert.That(track.LapCount, Is.EqualTo(1));
        }

        /// <summary>Checks station stops and waypoint hooks using two arrivals and records committed event state.</summary>
        [Test]
        public void Arrival_StopsAtStationsAndPublishesWaypointAndPositionEvents()
        {
            var track = CreateTrack();
            var positions = new List<string>();
            int waypoints = 0;
            track.PositionChanged += node => positions.Add(node.Id);
            track.WaypointReached += node => { waypoints++; Assert.That(track.CurrentNode, Is.SameAs(node)); };
            Assert.That(track.IsStopped, Is.True);
            track.TryDepart();
            track.Arrive();
            Assert.That(track.IsStopped, Is.False);
            track.TryDepart();
            track.Arrive();
            Assert.That(track.IsStopped, Is.True);
            Assert.That(waypoints, Is.EqualTo(1));
            Assert.That(positions, Is.EqualTo(new[] { "road", "home" }));
            Assert.Throws<InvalidOperationException>(() => track.Arrive());
        }

        /// <summary>Checks arbitrary station counts from supplied data and preserves topology over repeated laps.</summary>
        [TestCase(1)]
        [TestCase(4)]
        [TestCase(5)]
        public void Topology_IsImmutableAndStationCountComesFromData(int stationCount)
        {
            var source = new List<TrackNode>();
            for (int i = 0; i < stationCount; i++) source.Add(new TrackNode("station-" + i, true));
            source.Add(new TrackNode("waypoint", false));
            var track = new RingTrack(source);
            source.Clear();
            Assert.That(track.StationCount, Is.EqualTo(stationCount));
            Assert.Throws<NotSupportedException>(() => ((IList<TrackNode>)track.Nodes).RemoveAt(0));
            for (int i = 0; i < (stationCount + 1) * 3; i++) { track.TryDepart(); track.Arrive(); }
            Assert.That(track.Nodes.Count, Is.EqualTo(stationCount + 1));
            Assert.That(track.LapCount, Is.EqualTo(3));
        }

        /// <summary>Rejects invalid external topology inputs before any mutable movement state is created.</summary>
        [Test]
        public void InvalidTopology_IsRejected()
        {
            Assert.Throws<ArgumentException>(() => new RingTrack(Array.Empty<TrackNode>()));
            Assert.Throws<ArgumentException>(() => new RingTrack(new[] { new TrackNode("same", true), new TrackNode("same", false) }));
            Assert.Throws<ArgumentException>(() => new TrackNode("", true));
        }

        /// <summary>Loads the shipped external track asset and verifies ordered topology and manual departure defaults.</summary>
        [Test]
        public void ShippedJson_LoadsAuthoredTopologyAndTiming()
        {
            TextAsset json = UnityEngine.Resources.Load<TextAsset>("Track/ring-track");
            Assert.That(json, Is.Not.Null);
            TrackData data = TrackData.Parse(json.text);
            RingTrack track = data.CreateTrack();
            Assert.That(track.Nodes.Count, Is.EqualTo(data.Nodes.Count));
            Assert.That(track.StationCount, Is.EqualTo(4));
            Assert.That(data.EdgeSeconds, Is.EqualTo(2f));
            Assert.That(data.AutoDepart, Is.False);
            for (int i = 0; i < track.Nodes.Count; i++)
            {
                Assert.That(track.Nodes[i].Id, Is.EqualTo(data.Nodes[i].Id));
                Assert.That(track.Nodes[i].IsStation, Is.EqualTo(data.Nodes[i].IsStation));
            }
        }

        /// <summary>Checks that a one-node ring stays connected and increments laps once per explicit completed edge.</summary>
        [Test]
        public void SingleNodeRing_RemainsConnected()
        {
            var track = new RingTrack(new[] { new TrackNode("only", true) });
            track.TryDepart();
            track.Arrive();
            Assert.That(track.CurrentIndex, Is.Zero);
            Assert.That(track.NextIndex, Is.Zero);
            Assert.That(track.LapCount, Is.EqualTo(1));
        }

        /// <summary>Stacks repeated and independent hold reasons and checks that all releases are needed before departure.</summary>
        [Test]
        public void Holds_StackAndPreventDepartureUntilAllReleased()
        {
            RingTrack track = CreateTrack();
            track.RequestHold("combat");
            track.RequestHold("combat");
            track.RequestHold("event");
            track.Depart();
            Assert.That(track.IsMoving, Is.False);
            track.ReleaseHold("combat");
            track.ReleaseHold("event");
            Assert.That(track.TryDepart(), Is.False);
            track.ReleaseHold("combat");
            Assert.That(track.TryDepart(), Is.True);
            track.RequestHold("pause");
            Assert.Throws<InvalidOperationException>(() => track.Arrive());
            Assert.That(track.CurrentIndex, Is.Zero);
            track.ReleaseHold("pause");
            track.Arrive();
            Assert.That(track.CurrentNode.Id, Is.EqualTo("road"));
        }

        /// <summary>Exercises the shared interface with two arrivals and verifies ordered identities, hold state and lap events.</summary>
        [Test]
        public void SharedContract_ExposesNodesArrivalsAndCompletedLaps()
        {
            RingTrack model = CreateTrack();
            ITrackRuntime runtime = model;
            var arrived = new List<string>();
            var laps = new List<int>();
            runtime.Arrived += node => arrived.Add(node.Id);
            runtime.LapCompleted += lap => laps.Add(lap);
            Assert.That(runtime.Nodes[0].Index, Is.Zero);
            Assert.That(runtime.Nodes[1].Index, Is.EqualTo(1));
            Assert.That(runtime.Nodes[0].Kind, Is.EqualTo(TrackNodeKind.Hub));
            Assert.That(runtime.Nodes[0].HubId, Is.EqualTo("home"));
            Assert.That(runtime.Nodes[1].Kind, Is.EqualTo(TrackNodeKind.Waypoint));
            Assert.That(runtime.Nodes[1].HubId, Is.Null);
            Assert.That(runtime.IsHeld, Is.True);
            runtime.Depart();
            model.Arrive();
            Assert.That(runtime.CurrentNode.Id, Is.EqualTo("road"));
            Assert.That(runtime.IsHeld, Is.False);
            runtime.Depart();
            model.Arrive();
            Assert.That(runtime.Lap, Is.EqualTo(1));
            Assert.That(arrived, Is.EqualTo(new[] { "road", "home" }));
            Assert.That(laps, Is.EqualTo(new[] { 1 }));
        }
    }
}
