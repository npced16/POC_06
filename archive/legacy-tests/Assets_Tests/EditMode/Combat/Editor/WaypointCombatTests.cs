using System.IO;

using UnityEngine;

using NUnit.Framework;

using IronRing.Combat.Demo;
using IronRing.Contracts;

namespace IronRing.Combat.Tests
{
    public sealed class WaypointCombatTests
    {
        private EncounterData _data;
        private CombatDemoTrack _track;
        private CombatDemoInventory _inventory;
        private CombatDemoHubs _hubs;
        private CombatDemoFacilities _facilities;
        private CombatDemoNode _waypoint;

        /// <summary>Loads actual encounter JSON and prepares interface fakes with a wraparound next hub.</summary>
        [SetUp]
        public void SetUp()
        {
            _data = JsonUtility.FromJson<EncounterData>(File.ReadAllText("Assets/Scripts/Combat/Resources/Story005Encounters.json"));
            _waypoint = new CombatDemoNode { Id = "waypoint", Kind = TrackNodeKind.Waypoint, Index = 1 };
            _track = new CombatDemoTrack(new CombatDemoNode { Id = "hub", HubId = "next", Kind = TrackNodeKind.Hub }, _waypoint);
            _inventory = new CombatDemoInventory();
            _hubs = new CombatDemoHubs(50, "next");
            _facilities = new CombatDemoFacilities();
        }

        /// <summary>Verifies arrival wiring, three visible stages, reward, notifications and hold release for a win.</summary>
        [Test]
        public void Arrival_Win_AfterThreeRounds_RewardsAndReleases()
        {
            var resolver = Create(0, 0.5f, 0f, 0.99f, 0f);
            int started = 0, ended = 0;
            resolver.EncounterStarted += node => { started++; Assert.IsTrue(_track.IsHeld); };
            resolver.EncounterEnded += (node, outcome) => { ended++; Assert.IsFalse(_track.IsHeld); };
            _track.Arrived += resolver.OnWaypointArrived;
            _track.Arrive(_waypoint);
            Assert.AreEqual(1, started);
            for (int i = 0; i < 2; i++) { resolver.Advance(); Assert.IsTrue(_track.IsHeld); }
            resolver.Advance(); resolver.Advance();
            Assert.AreEqual(EncounterOutcome.Victory, resolver.LastOutcome);
            Assert.AreEqual(3, resolver.Combat.Round);
            Assert.AreEqual(3, _inventory.Get("scrap"));
            Assert.AreEqual(1, ended);
            Assert.IsFalse(resolver.IsEncounterActive);
        }

        /// <summary>Verifies a defeat reduces only the random resource and the next hub's prosperity.</summary>
        [Test]
        public void Defeat_AppliesDataPenaltyToNextHub()
        {
            _inventory.Add(_data.PenaltyResources[1], 5);
            var resolver = Create(1, 0.9f, 0.99f, 0.99f, 0.99f);
            resolver.OnWaypointArrived(_waypoint);
            for (int i = 0; i < 3; i++) resolver.Advance();
            Assert.AreEqual(EncounterOutcome.Defeat, resolver.LastOutcome);
            Assert.AreEqual(3, _inventory.Get(_data.PenaltyResources[1]));
            Assert.AreEqual(40, _hubs.GetProsperity("next"));
            Assert.AreEqual("next", _hubs.LastChangedHub);
            Assert.IsFalse(_track.IsHeld);
        }

        /// <summary>Verifies each externally defined sample event changes its specified stock and ends its hold.</summary>
        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
        public void Events_ApplyExternalResourceDelta(int index)
        {
            EncounterEvent entry = _data.Events[index];
            _inventory.Add(entry.Resource, 5);
            var resolver = Create(index, 0.499f);
            resolver.OnWaypointArrived(_waypoint);
            Assert.IsNull(resolver.Combat);
            Assert.IsTrue(_track.IsHeld);
            resolver.Advance();
            Assert.AreEqual(5 + entry.Delta, _inventory.Get(entry.Resource));
            Assert.AreEqual(EncounterOutcome.EventResolved, resolver.LastOutcome);
            Assert.IsFalse(_track.IsHeld);
        }

        /// <summary>Verifies low stock clamps penalties and another system's hold survives encounter completion.</summary>
        [Test]
        public void Loss_ClampsStock_AndPreservesOtherHold()
        {
            _inventory.Add("scrap", 1);
            _track.RequestHold("other");
            var resolver = Create(0, 0.5f, 0.99f, 0.99f, 0.99f);
            resolver.OnWaypointArrived(_waypoint);
            resolver.OnWaypointArrived(_waypoint);
            for (int i = 0; i < 3; i++) resolver.Advance();
            Assert.AreEqual(0, _inventory.Get("scrap"));
            Assert.IsTrue(_track.IsHeld);
            _track.ReleaseHold("other");
            Assert.IsFalse(_track.IsHeld);
        }

        /// <summary>Verifies facility bonus is requested for the wrapped next hub and changes the round threshold.</summary>
        [Test]
        public void FacilityBonus_UsesNextHubAndChangesRound()
        {
            _facilities.Bonus = 10;
            var resolver = Create(0, 0.5f, 0.65f);
            resolver.OnWaypointArrived(_waypoint); resolver.Advance();
            Assert.AreEqual("next", _facilities.LastQueriedHub);
            Assert.AreEqual(20, resolver.Combat.TrainStrength);
            Assert.IsTrue(resolver.Combat.LastRoundWon);
        }

        /// <summary>Verifies hub arrivals do nothing and disposing a combat releases its own movement hold.</summary>
        [Test]
        public void HubIgnored_DisposeReleasesHold()
        {
            var resolver = Create(0, 0.5f);
            resolver.OnWaypointArrived(_track.Nodes[0]);
            Assert.IsFalse(resolver.IsEncounterActive);
            resolver.OnWaypointArrived(_waypoint); resolver.Dispose();
            Assert.IsFalse(_track.IsHeld);
            resolver.OnWaypointArrived(_waypoint);
            Assert.IsFalse(resolver.IsEncounterActive);
        }

        /// <summary>Verifies the same roll selects different encounter kinds when the external ratio changes.</summary>
        [TestCase(0f, false)] [TestCase(1f, true)]
        public void Ratio_ComesFromData(float ratio, bool expectEvent)
        {
            string json = File.ReadAllText("Assets/Scripts/Combat/Resources/Story005Encounters.json");
            _data = JsonUtility.FromJson<EncounterData>(json.Replace("0.5", ratio == 0 ? "0.0" : "1.0"));
            var resolver = Create(0, 0.5f);
            resolver.OnWaypointArrived(_waypoint);
            Assert.AreEqual(expectEvent, resolver.Combat == null);
        }

        /// <summary>Verifies a mid-ring waypoint targets the following hub rather than the first hub.</summary>
        [Test]
        public void NextHub_UsesFollowingRingNode()
        {
            _track = new CombatDemoTrack(_track.Nodes[0], _waypoint,
                new CombatDemoNode { Id = "second", HubId = "second", Kind = TrackNodeKind.Hub });
            _hubs = new CombatDemoHubs(50, "next", "second");
            var resolver = Create(0, 0.5f, 0.99f, 0.99f, 0.99f);
            resolver.OnWaypointArrived(_waypoint);
            for (int i = 0; i < 3; i++) resolver.Advance();
            Assert.AreEqual("second", _facilities.LastQueriedHub);
            Assert.AreEqual(40, _hubs.GetProsperity("second"));
            Assert.AreEqual(50, _hubs.GetProsperity("next"));
        }

        /// <summary>Verifies completed combat consumes no extra random rolls or resource rewards on repeated advancement.</summary>
        [Test]
        public void CompletedCombat_RejectsExtraRounds()
        {
            var combat = new CombatEncounter(_data, 0, new CombatDemoRandom(0, 0f, 0f, 0f));
            for (int i = 0; i < 3; i++) Assert.IsTrue(combat.AdvanceRound());
            Assert.IsFalse(combat.AdvanceRound());
            Assert.AreEqual(3, combat.Wins);
        }

        /// <summary>Creates a resolver using the supplied scripted random values and prepared interface fakes.</summary>
        private WaypointResolver Create(int integer, params float[] rolls) =>
            new WaypointResolver(_data, _track, _inventory, _hubs, _facilities, new CombatDemoRandom(integer, rolls));
    }
}
