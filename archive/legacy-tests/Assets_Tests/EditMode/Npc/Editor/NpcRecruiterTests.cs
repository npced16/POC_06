using System;
using System.Linq;

using NUnit.Framework;

using IronRing.Contracts;

namespace IronRing.Npc.Tests
{
    public sealed class NpcRecruiterTests
    {
        /// <summary>Creates an immutable NPC fixture from an id and allowed hubs for eligibility tests.</summary>
        private static NpcDefinition Npc(string id, params string[] hubs) => new NpcDefinition(id, id, "Keep the ring moving.", hubs, true);

        /// <summary>Checks flag and habitat input combinations and the resulting roll count.</summary>
        [TestCase(false, true)]
        [TestCase(true, false)]
        public void IneligibleVisitDoesNotRoll(bool flagged, bool complete)
        {
            var random = new FakeRandom();
            var recruiter = new NpcRecruiter(new[] { Npc("a", "hub-1") }, 0.5f, new FakeHabitat(flagged, complete), random);
            recruiter.OnHubVisited("hub-1");
            Assert.That(random.FloatCalls, Is.Zero);
            Assert.That(recruiter.NpcsAt("hub-1"), Is.Empty);
        }

        /// <summary>Checks injected probability boundary values and resulting membership.</summary>
        [TestCase(0.499f, 1)]
        [TestCase(0.5f, 0)]
        [TestCase(0.999f, 0)]
        public void ChanceUsesStrictBoundary(float roll, int expected)
        {
            var random = new FakeRandom(roll);
            var recruiter = new NpcRecruiter(new[] { Npc("a", "hub-1") }, 0.5f, new FakeHabitat(), random);
            recruiter.OnHubVisited("hub-1");
            Assert.That(recruiter.NpcsAt("hub-1").Count, Is.EqualTo(expected));
            Assert.That(random.FloatCalls, Is.EqualTo(1));
        }

        /// <summary>Checks one eligible visit emits one event after membership is visible, despite reentrant callbacks.</summary>
        [Test]
        public void VisitJoinsAtMostOneAndPublishesCommittedMembership()
        {
            var random = new FakeRandom();
            var recruiter = new NpcRecruiter(new[] { Npc("a", "hub-1"), Npc("b", "hub-1") }, 0.5f, new FakeHabitat(), random);
            int events = 0;
            recruiter.NpcJoined += (hub, id) => { events++; Assert.That(recruiter.NpcsAt(hub), Does.Contain(id)); recruiter.OnHubVisited(hub); };
            recruiter.OnHubVisited("hub-1");
            Assert.That(events, Is.EqualTo(1));
            Assert.That(random.FloatCalls, Is.EqualTo(1));
            Assert.That(recruiter.NpcsAt("hub-1").Count, Is.EqualTo(1));
        }

        /// <summary>Visits different hubs with a shared eligible NPC and verifies global uniqueness and exhausted no-op.</summary>
        [Test]
        public void JoinedNpcNeverAppearsAtAnotherHubAndExhaustionDoesNotRoll()
        {
            var random = new FakeRandom();
            var recruiter = new NpcRecruiter(new[] { Npc("a", "hub-1", "hub-2") }, 0.5f, new FakeHabitat(), random);
            recruiter.OnHubVisited("hub-1");
            recruiter.OnHubVisited("hub-2");
            recruiter.OnHubVisited("hub-1");
            Assert.That(recruiter.NpcsAt("hub-1"), Is.EqualTo(new[] { "a" }));
            Assert.That(recruiter.NpcsAt("hub-2"), Is.Empty);
            Assert.That(random.FloatCalls, Is.EqualTo(1));
        }

        /// <summary>Checks empty and unmatched pools consume no random values and cause no joins.</summary>
        [Test]
        public void EmptyOrUnmatchedPoolIsNoOp()
        {
            var random = new FakeRandom();
            new NpcRecruiter(Array.Empty<NpcDefinition>(), 0.5f, new FakeHabitat(), random).OnHubVisited("hub-1");
            new NpcRecruiter(new[] { Npc("a", "hub-2") }, 0.5f, new FakeHabitat(), random).OnHubVisited("hub-1");
            Assert.That(random.FloatCalls, Is.Zero);
        }

        /// <summary>Repeats identical injected input streams and verifies deterministic selection and later visits.</summary>
        [Test]
        public void RepeatedInputsSelectSameNpcAndLaterVisitCanJoinAgain()
        {
            foreach (int run in new[] { 0, 1 })
            {
                var recruiter = new NpcRecruiter(new[] { Npc("a", "hub-1"), Npc("b", "hub-1") }, 0.5f, new FakeHabitat(), new FakeRandom(0.1f, 1));
                recruiter.OnHubVisited("hub-1");
                Assert.That(recruiter.NpcsAt("hub-1"), Is.EqualTo(new[] { "b" }));
                recruiter.OnHubVisited("hub-1");
                Assert.That(recruiter.NpcsAt("hub-1"), Is.EqualTo(new[] { "b", "a" }));
            }
        }

        /// <summary>Checks duplicate data ids are rejected before global identity can become ambiguous.</summary>
        [Test]
        public void DuplicateIdsAreRejected() => Assert.Throws<ArgumentException>(() => new NpcRecruiter(new[] { Npc("a", "hub-1"), Npc("a", "hub-2") }, 0.5f, new FakeHabitat(), new FakeRandom()));

        /// <summary>Loads serialized external-style data and checks probability, identity, quote and habitat fields.</summary>
        [Test]
        public void JsonDataDefinesPoolAndChance()
        {
            const string json = "{\"_joinChance\":0.25,\"_npcs\":[{\"_id\":\"smith\",\"_name\":\"Ada\",\"_quote\":\"Iron holds.\",\"_eligibleHubIds\":[\"hub-x\"],\"_requiresCompleteHabitat\":true}]}";
            var data = NpcPoolData.FromJson(json);
            Assert.That(data.JoinChance, Is.EqualTo(0.25f));
            var npc = data.Definitions.Single();
            Assert.That(npc.Id, Is.EqualTo("smith"));
            Assert.That(npc.Name, Is.EqualTo("Ada"));
            Assert.That(npc.Quote, Is.EqualTo("Iron holds."));
            Assert.That(npc.EligibleHubIds, Is.EqualTo(new[] { "hub-x" }));
            Assert.That(npc.RequiresCompleteHabitat, Is.True);
        }

        private sealed class FakeRandom : IRandomSource
        {
            private readonly float _roll;
            private readonly int _index;
            public int FloatCalls { get; private set; }
            /// <summary>Stores deterministic probability and selection inputs for subsequent draws.</summary>
            public FakeRandom(float roll = 0f, int index = 0) { _roll = roll; _index = index; }
            /// <summary>Returns the injected roll and increments the observed draw count.</summary>
            public float NextFloat() { FloatCalls++; return _roll; }
            /// <summary>Returns the injected index bounded to the requested eligible range.</summary>
            public int NextInt(int minInclusive, int maxExclusive) => Math.Min(minInclusive + _index, maxExclusive - 1);
        }

        private sealed class FakeHabitat : IHabitatSystem
        {
            private readonly bool _flagged;
            private readonly bool _complete;
            public event Action<string> EventFlagRaised { add { } remove { } }
            /// <summary>Stores fixed flag and completion inputs for isolated recruiter tests.</summary>
            public FakeHabitat(bool flagged = true, bool complete = true) { _flagged = flagged; _complete = complete; }
            /// <summary>Returns the configured flag for the queried hub without changing habitat.</summary>
            public bool IsEventFlagSet(string hubId) => _flagged;
            /// <summary>Returns the configured completion for the queried hub without changing habitat.</summary>
            public bool IsHabitatComplete(string hubId) => _complete;
            /// <summary>Refuses placement for all hub, slot and resource inputs in this read-only fake.</summary>
            public bool TryPlace(string hubId, int slotIndex, string resourceId) => false;
            /// <summary>Refuses removal for all hub and slot inputs in this read-only fake.</summary>
            public bool TryRemove(string hubId, int slotIndex) => false;
            /// <summary>Returns no resource for all hub and slot inputs in this fake.</summary>
            public string SlotContent(string hubId, int slotIndex) => null;
        }
    }
}
