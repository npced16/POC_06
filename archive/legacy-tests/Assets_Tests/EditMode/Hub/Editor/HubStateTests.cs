using System;
using System.Collections.Generic;

using UnityEngine;

using UnityEditor;

using NUnit.Framework;

using IronRing.Contracts;
using IronRing.Hub.Demo;

namespace IronRing.Hub.Tests
{
    /// <summary>Story 004 + 014: prosperity, phases (Ruined, Recovering, Settled, Collapsed), lap rule, deadline and both run-loss rules.</summary>
    public sealed class HubStateTests
    {
        private const float Settle = 40f;

        /// <summary>Story 014 test profile matching the shipped rule values (decay -3, NPC +3, facility +2, item +1, habitat +5).</summary>
        private static HubDefinition Def(string id, float initial = 0f, HubRunRules rules = null) =>
            new HubDefinition(id, 0, 100, initial, 0, -3, 3, 2, 1, 5, Settle, rules);

        /// <summary>Creates a run over the given definitions with fake population and sources.</summary>
        private static RunState Run(HubDemoPopulation population, FakeSources sources, params HubDefinition[] definitions)
        {
            var state = new RunState(definitions, population, population);
            state.AttachSources(sources);
            return state;
        }

        private sealed class FakeSources : IProsperitySources
        {
            public readonly Dictionary<string, int> Items = new Dictionary<string, int>();
            public readonly HashSet<string> Habitats = new HashSet<string>();
            public int PlacedItemCount(string hubId) => Items.TryGetValue(hubId, out int n) ? n : 0;
            public bool IsHabitatComplete(string hubId) => Habitats.Contains(hubId);
        }

        // ---------- start state and data ----------

        [Test]
        public void ShippedProfiles_StartRuinedAtZeroWithEightLapDeadline()
        {
            TextAsset json = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Scripts/Hub/Data/HubProfiles.json");
            var fake = new HubDemoPopulation();

            var state = new RunState(HubData.Parse(json.text), fake, fake);

            foreach (string id in state.HubIds)
            {
                Assert.That(state.GetPhase(id), Is.EqualTo(HubPhase.Ruined));
                Assert.That(state.GetProsperity(id), Is.EqualTo(0));
                Assert.That(state.IsRuined(id), Is.True);
                Assert.That(state.HasEverSettled(id), Is.False);
            }
            Assert.That(state.IsRunLost, Is.False, "All-ruined start must not lose the run.");
            Assert.That(state.LapsUntilDeadline, Is.EqualTo(8));
        }

        [Test]
        public void ShippedProfiles_LapRuleKeepsGrowthMultipliers()
        {
            TextAsset json = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Scripts/Hub/Data/HubProfiles.json");
            var fake = new HubDemoPopulation();
            var sources = new FakeSources();
            var state = new RunState(HubData.Parse(json.text), fake, fake);
            state.AttachSources(sources);
            foreach (string id in state.HubIds) { state.ApplyDelta(id, 20, "seed"); fake.SetCounts(id, 1, 1); sources.Items[id] = 2; }
            sources.Habitats.Add("hub-1");

            state.OnLapCompleted(1);

            // 20 - 3*decay + 3*npc + 2*facility + 2 items (+5 habitat on hub-1)
            Assert.That(state.GetProsperity("hub-1"), Is.EqualTo(20 - 3 + 3 + 3 + 2 + 5));
            Assert.That(state.GetProsperity("hub-2"), Is.EqualTo(20 - 3 + 4.5f + 2 + 2));
            Assert.That(state.GetProsperity("hub-3"), Is.EqualTo(20 - 1.5f + 3 + 2 + 2));
            Assert.That(state.GetProsperity("hub-4"), Is.EqualTo(20 - 4.5f + 3 + 2 + 2));
            Assert.That(state.GetPhase("hub-1"), Is.EqualTo(HubPhase.Settled), "Habitat completion settles below the threshold.");
            Assert.That(state.GetPhase("hub-2"), Is.EqualTo(HubPhase.Recovering));
        }

        [Test]
        public void ParseWithoutDeadline_Throws()
        {
            const string json = "{\"_hubs\":[{\"_id\":\"a\",\"_maximum\":100,\"_settledThreshold\":40,\"_decayScale\":1,\"_npcScale\":1,\"_facilityScale\":1}]}";

            Assert.Throws<ArgumentOutOfRangeException>(() => HubData.Parse(json));
        }

        // ---------- lap rule ----------

        [Test]
        public void RuinedHubLap_SkipsBaseDecayAndAddsItems()
        {
            var fake = new HubDemoPopulation();
            var sources = new FakeSources();
            var state = Run(fake, sources, Def("a"));

            state.OnLapCompleted(1);
            Assert.That(state.GetProsperity("a"), Is.EqualTo(0));
            Assert.That(state.GetPhase("a"), Is.EqualTo(HubPhase.Ruined));

            sources.Items["a"] = 2;
            state.OnLapCompleted(2);

            Assert.That(state.GetProsperity("a"), Is.EqualTo(2), "Ruined applies no -3 base decay.");
            Assert.That(state.GetPhase("a"), Is.EqualTo(HubPhase.Recovering));
        }

        [Test]
        public void RuinedHubNegativeDelta_IsIgnored()
        {
            var state = Run(new HubDemoPopulation(), new FakeSources(), Def("a"));
            var events = new List<string>();
            state.ProsperityChanged += (id, value) => events.Add(id + ":" + value);

            state.ApplyDelta("a", -10, "combat penalty");

            Assert.That(state.GetProsperity("a"), Is.EqualTo(0));
            Assert.That(events, Is.Empty);
        }

        [Test]
        public void RecoveringLap_AppliesFullRuleThroughInterfaces()
        {
            var fake = new HubDemoPopulation();
            var sources = new FakeSources();
            var state = Run(fake, sources, Def("a", 10));
            fake.SetCounts("a", 2, 3);
            sources.Items["a"] = 4;

            state.OnLapCompleted(1);

            Assert.That(state.GetProsperity("a"), Is.EqualTo(10 - 3 + 2 * 3 + 3 * 2 + 4 * 1));
        }

        [Test]
        public void DuplicateOrOldLap_DoesNothing()
        {
            var fake = new HubDemoPopulation();
            var state = Run(fake, new FakeSources(), Def("a", 10, new HubRunRules(5)));
            state.OnLapCompleted(2);

            state.OnLapCompleted(2);
            state.OnLapCompleted(1);

            Assert.That(state.GetProsperity("a"), Is.EqualTo(7));
            Assert.That(state.LapsUntilDeadline, Is.EqualTo(4));
            Assert.That(state.LastLap, Is.EqualTo(2));
        }

        // ---------- phase transitions ----------

        [Test]
        public void PhaseTransitions_RaisePhaseChangedInOrder()
        {
            var state = Run(new HubDemoPopulation(), new FakeSources(), Def("a"), Def("b"));
            var phases = new List<HubPhase>();
            state.PhaseChanged += (id, phase) => { if (id == "a") phases.Add(phase); };

            state.ApplyDelta("a", 5, "items");
            state.ApplyDelta("a", 35, "growth");
            state.ApplyDelta("a", -39, "neglect");
            Assert.That(state.GetPhase("a"), Is.EqualTo(HubPhase.Settled), "Settled holds below the threshold until 0.");
            state.ApplyDelta("a", -1, "neglect");

            Assert.That(phases, Is.EqualTo(new[] { HubPhase.Recovering, HubPhase.Settled, HubPhase.Collapsed }));
            Assert.That(state.IsRuined("a"), Is.True);
            Assert.That(state.HasEverSettled("a"), Is.True);
        }

        [Test]
        public void CollapsedHub_IgnoresEveryChange()
        {
            var fake = new HubDemoPopulation();
            var state = Run(fake, new FakeSources(), Def("a", Settle), Def("b", Settle));
            state.ApplyDelta("a", -100, "raid");

            state.ApplyDelta("a", 50, "heal");
            fake.SetCounts("a", 5, 5);
            state.OnLapCompleted(1);

            Assert.That(state.GetPhase("a"), Is.EqualTo(HubPhase.Collapsed));
            Assert.That(state.GetProsperity("a"), Is.EqualTo(0));
        }

        [Test]
        public void RecoveringFallingToZero_ReturnsToRuinedAndRaisesHubRuined()
        {
            var state = Run(new HubDemoPopulation(), new FakeSources(), Def("a"));
            var events = new List<string>();
            state.PhaseChanged += (id, phase) => events.Add(phase.ToString());
            state.HubRuined += id => events.Add("ruined:" + id);

            state.ApplyDelta("a", 5, "items");
            state.ApplyDelta("a", -5, "raid");

            Assert.That(state.GetPhase("a"), Is.EqualTo(HubPhase.Ruined));
            Assert.That(events, Is.EqualTo(new[] { "Recovering", "Ruined", "ruined:a" }));
            Assert.That(state.IsRunLost, Is.False);
        }

        [Test]
        public void HabitatCompleteOnRuinedHub_SettlesInOneLap()
        {
            var sources = new FakeSources();
            var state = Run(new HubDemoPopulation(), sources, Def("a"));
            var phases = new List<HubPhase>();
            state.PhaseChanged += (id, phase) => phases.Add(phase);
            sources.Habitats.Add("a");

            state.OnLapCompleted(1);

            Assert.That(state.GetProsperity("a"), Is.EqualTo(5));
            Assert.That(phases, Is.EqualTo(new[] { HubPhase.Recovering, HubPhase.Settled }));
        }

        [Test]
        public void ThresholdExactlyReached_Settles()
        {
            var state = Run(new HubDemoPopulation(), new FakeSources(), Def("a", 1));

            state.ApplyDelta("a", Settle - 1, "growth");

            Assert.That(state.GetPhase("a"), Is.EqualTo(HubPhase.Settled));
        }

        // ---------- deadline and run loss ----------

        [Test]
        public void DeadlineCountdown_LosesRunWhenNothingSettles()
        {
            var state = Run(new HubDemoPopulation(), new FakeSources(), Def("a", 0, new HubRunRules(3)), Def("b", 0, new HubRunRules(3)));
            int lost = 0;
            state.RunLost += () => lost++;

            state.OnLapCompleted(1);
            state.OnLapCompleted(2);
            Assert.That(state.LapsUntilDeadline, Is.EqualTo(1));
            Assert.That(state.IsRunLost, Is.False);
            state.OnLapCompleted(3);
            state.OnLapCompleted(4);

            Assert.That(state.LapsUntilDeadline, Is.EqualTo(0));
            Assert.That(state.IsRunLost, Is.True);
            Assert.That(state.LastLossReason, Is.EqualTo(RunLossReason.DeadlineExpired));
            Assert.That(lost, Is.EqualTo(1));
        }

        [Test]
        public void SettlingOnDeadlineLap_IsJudgedBeforeTheDeadline()
        {
            var sources = new FakeSources();
            var state = Run(new HubDemoPopulation(), sources, Def("a", 0, new HubRunRules(2)));
            state.OnLapCompleted(1);
            sources.Habitats.Add("a");

            state.OnLapCompleted(2);
            state.OnLapCompleted(3);

            Assert.That(state.GetPhase("a"), Is.EqualTo(HubPhase.Settled));
            Assert.That(state.LapsUntilDeadline, Is.EqualTo(0));
            Assert.That(state.IsRunLost, Is.False, "After a settlement the deadline no longer loses the run.");
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(4)]
        public void EverySettledHubCollapsing_LosesRunIgnoringNeverSettledHubs(int settledCount)
        {
            var definitions = new List<HubDefinition>();
            for (int i = 0; i < settledCount; i++) definitions.Add(Def("s" + i, Settle));
            definitions.Add(Def("never"));
            var state = Run(new HubDemoPopulation(), new FakeSources(), definitions.ToArray());
            int lost = 0;
            state.RunLost += () => lost++;

            for (int i = 0; i < settledCount - 1; i++) state.ApplyDelta("s" + i, -100, "raid");
            Assert.That(state.IsRunLost, Is.False);
            state.ApplyDelta("s" + (settledCount - 1), -100, "raid");

            Assert.That(state.IsRunLost, Is.True);
            Assert.That(state.LastLossReason, Is.EqualTo(RunLossReason.AllSettledCollapsed));
            Assert.That(state.GetPhase("never"), Is.EqualTo(HubPhase.Ruined));
            Assert.That(lost, Is.EqualTo(1));
        }

        [Test]
        public void EventOrder_ProsperityThenPhaseThenRuinThenLoss()
        {
            var state = Run(new HubDemoPopulation(), new FakeSources(), Def("a", Settle));
            var events = new List<string>();
            state.ProsperityChanged += (id, value) => events.Add(id + ":" + value);
            state.PhaseChanged += (id, phase) => events.Add(phase.ToString());
            state.HubRuined += id => events.Add("ruin:" + id);
            state.RunLost += () => events.Add("lost");

            state.ApplyDelta("a", -100, "raid");

            Assert.That(events, Is.EqualTo(new[] { "a:0", "Collapsed", "ruin:a", "lost" }));
        }

        [Test]
        public void HubIdentities_PersistThroughRuinAndCollapse()
        {
            var fake = new HubDemoPopulation();
            var state = Run(fake, new FakeSources(), Def("a", Settle), Def("b"), Def("c"), Def("d"));
            var original = new List<string>(state.HubIds);

            state.ApplyDelta("b", 5, "items");
            state.ApplyDelta("b", -5, "raid");
            foreach (string id in original) { fake.OnHubVisited(id); Assert.DoesNotThrow(() => state.GetProsperity(id)); }

            Assert.That(state.HubIds, Is.EqualTo(original));
            Assert.That(state.IsRuined("b"), Is.True);
        }

        [Test]
        public void InvalidData_IsRejected()
        {
            var fake = new HubDemoPopulation();
            Assert.Throws<ArgumentException>(() => new RunState(Array.Empty<HubDefinition>(), fake, fake));
            var definition = Def("a");
            Assert.Throws<ArgumentException>(() => new RunState(new[] { definition, definition }, fake, fake));
            Assert.Throws<ArgumentException>(() => new HubDefinition("a", 0, 100, float.NaN, 0, -3, 3, 2));
            Assert.Throws<ArgumentException>(() => new HubDefinition("a", 0, 100, 0, 0, -3, 3, 2, 1, 5, 0, null));
            Assert.Throws<ArgumentException>(() => new HubDefinition("a", 0, 100, 0, 0, -3, 3, 2, 1, 5, 101, null));
            Assert.Throws<ArgumentOutOfRangeException>(() => new HubRunRules(0));
            var state = Run(fake, new FakeSources(), Def("a", 10));
            Assert.Throws<ArgumentOutOfRangeException>(() => state.ApplyDelta("a", float.NaN, "invalid"));
            Assert.That(state.GetProsperity("a"), Is.EqualTo(10));
        }

        [Test]
        public void NoSourcesAttached_ReadsZeroItemsAndNoHabitat()
        {
            var fake = new HubDemoPopulation();
            var state = new RunState(new[] { Def("a") }, fake, fake);

            state.OnLapCompleted(1);

            Assert.That(state.GetProsperity("a"), Is.EqualTo(0));
            Assert.That(state.LapsUntilDeadline, Is.EqualTo(int.MaxValue), "No rules means no deadline.");
        }
    }
}
