using System;
using System.Linq;

using UnityEngine;

using UnityEditor;

using NUnit.Framework;

using IronRing.Bootstrap;
using IronRing.Contracts;
using IronRing.Hub;

namespace IronRing.Integration.Tests
{
    public sealed class IntegrationFlowTests
    {
        /// <summary>Loads the production JSON inputs and returns the same definition used by Main without creating a scene.</summary>
        private static GameDefinition Definition()
        {
            return GameDefinition.Parse(Read("Assets/Scripts/Track/Resources/Track/ring-track.json"),
                Read("Assets/Scripts/Habitat/Resources/Story002/habitats.json"), Read("Assets/Scripts/Npc/Data/NpcPool.json"),
                Read("Assets/Scripts/Hub/Data/HubProfiles.json"), Read("Assets/Scripts/Combat/Resources/Story005Encounters.json"),
                Read("Assets/Scripts/Defense/Data/Facilities.json"), Read("Assets/Scripts/Bootstrap/Data/MainSettings.json"));
        }

        /// <summary>Reads a project TextAsset at path and returns its JSON text for real configuration loading.</summary>
        private static string Read(string path) => AssetDatabase.LoadAssetAtPath<TextAsset>(path).text;

        /// <summary>Creates the real session and attaches Story 014's sources the same way the installer does (habitat from the session).</summary>
        private static GameSession Session(GameDefinition data)
        {
            var game = new GameSession(data, true);
            game.Hubs.AttachSources(new ServiceProsperitySources(() => null, () => game.Habitat));
            return game;
        }

        /// <summary>Completes one real track edge, resolves every displayed encounter step, and returns its outcome.</summary>
        private static EncounterOutcome? Travel(GameSession game)
        {
            Assert.That(game.Track.TryDepart(), Is.True);
            game.Track.Arrive();
            EncounterOutcome? outcome = null;
            if (game.Waypoints.IsEncounterActive)
            {
                Assert.That(game.Track.HasActiveHolds, Is.True);
                Assert.That(game.Track.TryDepart(), Is.False);
                while (game.Waypoints.IsEncounterActive) game.Waypoints.Advance();
                outcome = game.Waypoints.LastOutcome;
                Assert.That(game.Track.HasActiveHolds, Is.False);
            }
            return outcome;
        }

        /// <summary>
        /// Story 014 flow on real systems: every hub starts Ruined, ruins refuse construction and ignore penalties,
        /// a completed habitat settles the demo hub at the next lap, the NPC joins on the next visit, construction then
        /// succeeds, the lap rule matches the data, and collapsing the only settled hub loses the run.
        /// </summary>
        [Test]
        public void RealSystems_RuinedStartSettlesThroughHabitatAndCollapseLoses()
        {
            GameDefinition data = Definition();
            using GameSession game = Session(data);
            string hub = data.Settings.DemoHubId;
            foreach (string id in game.Hubs.HubIds)
            {
                Assert.That(game.Hubs.GetPhase(id), Is.EqualTo(HubPhase.Ruined));
                Assert.That(game.Hubs.GetProsperity(id), Is.EqualTo(0));
            }
            Assert.That(game.Hubs.IsRunLost, Is.False);
            Assert.That(game.Hubs.LapsUntilDeadline, Is.EqualTo(data.HubDefinitions[0].Rules.DeadlineLaps));

            int scrap = game.Inventory.Get(data.Encounters.RewardResource);
            Assert.That(Travel(game), Is.EqualTo(EncounterOutcome.Victory));
            Assert.That(game.Inventory.Get(data.Encounters.RewardResource), Is.EqualTo(scrap + data.Encounters.RewardAmount));
            Travel(game);
            Assert.That(game.Track.CurrentNode.HubId, Is.EqualTo(hub));
            Assert.That(game.PlaceRecipe(hub), Is.True);
            Assert.That(game.Habitat.IsEventFlagSet(hub), Is.True);
            Assert.That(game.Npcs.NpcsAt(hub), Is.Empty, "Raising the flag must not recruit during the same visit.");
            Assert.That(game.Facilities.TryBuild(hub, data.Settings.DemoFacilityId), Is.False, "Story 006: no construction in ruins.");

            var stockBeforeLoss = game.Inventory.Snapshot().Values.Sum();
            string penaltyHub = game.Track.Nodes[(game.Track.CurrentIndex + 2) % game.Track.Nodes.Count].HubId;
            Assert.That(Travel(game), Is.EqualTo(EncounterOutcome.Defeat));
            Assert.That(game.Inventory.Snapshot().Values.Sum(), Is.EqualTo(stockBeforeLoss - data.Encounters.PenaltyAmount));
            Assert.That(game.Hubs.GetProsperity(penaltyHub), Is.EqualTo(0), "A Ruined hub never drops below 0.");

            bool visitedRuin = false;
            do
            {
                Travel(game);
                visitedRuin |= game.Track.CurrentNode.IsStation && game.Hubs.IsRuined(game.Track.CurrentNode.HubId);
            } while (game.Track.CurrentNode.HubId != hub);
            Assert.That(visitedRuin, Is.True, "Ruins stay on the ring and are visited.");
            Assert.That(game.Track.Lap, Is.EqualTo(1));
            Assert.That(game.Hubs.GetPhase(hub), Is.EqualTo(HubPhase.Settled), "Habitat completion settles at the lap tick.");
            Assert.That(game.Hubs.HasEverSettled(hub), Is.True);
            Assert.That(game.Hubs.LapsUntilDeadline, Is.EqualTo(data.HubDefinitions[0].Rules.DeadlineLaps - 1));
            Assert.That(game.Npcs.NpcsAt(hub).Count, Is.EqualTo(1));
            Assert.That(game.Facilities.TryBuild(hub, data.Settings.DemoFacilityId), Is.True);
            Assert.That(game.Facilities.FacilitiesAt(hub).Count, Is.EqualTo(1));

            int nodes = game.Track.Nodes.Count;
            while (game.Track.CurrentIndex != nodes - 1) Travel(game);
            var expectedAfterLap = data.HubDefinitions.ToDictionary(profile => profile.Id, profile =>
            {
                string id = profile.Id;
                HubPhase phase = game.Hubs.GetPhase(id);
                if (phase == HubPhase.Collapsed) return game.Hubs.GetProsperity(id);
                float delta = (phase == HubPhase.Ruined ? 0 : profile.BasePerLap)
                    + (game.Habitat.IsHabitatComplete(id) ? profile.HabitatBonus : 0)
                    + profile.PerNpc * game.Npcs.NpcsAt(id).Count + profile.PerFacility * game.Facilities.FacilitiesAt(id).Count;
                return Math.Max(profile.Minimum, Math.Min(profile.Maximum, game.Hubs.GetProsperity(id) + delta));
            });
            int previousLap = game.Track.Lap;
            Travel(game);
            Assert.That(game.Track.Lap, Is.EqualTo(previousLap + 1));
            foreach (var expected in expectedAfterLap)
                Assert.That(game.Hubs.GetProsperity(expected.Key), Is.EqualTo(expected.Value).Within(0.001f),
                    "Real lap arrival must apply the Story 014 lap rule for " + expected.Key);
            Assert.That(game.Track.Nodes.Count, Is.EqualTo(nodes));
            Assert.That(game.Hubs.IsRunLost, Is.False);

            game.Hubs.ApplyDelta(hub, -data.HubDefinitions.First(profile => profile.Id == hub).Maximum, "integration terminal damage");
            Assert.That(game.Hubs.GetPhase(hub), Is.EqualTo(HubPhase.Collapsed));
            Assert.That(game.Hubs.IsRunLost, Is.True);
            Assert.That(game.Hubs.LastLossReason, Is.EqualTo(RunLossReason.AllSettledCollapsed));
            Assert.That(game.Track.TryDepart(), Is.False, "Run loss holds the real track.");
        }

        /// <summary>Story 014 deadline on real systems: touring without settling any hub loses the run exactly at the data deadline lap.</summary>
        [Test]
        public void RealSystems_NoSettlementBeforeDeadline_LosesRun()
        {
            GameDefinition data = Definition();
            using GameSession game = Session(data);
            int deadline = data.HubDefinitions[0].Rules.DeadlineLaps;

            while (!game.Hubs.IsRunLost)
            {
                Assert.That(game.Track.Lap, Is.LessThan(deadline));
                Travel(game);
            }

            Assert.That(game.Track.Lap, Is.EqualTo(deadline));
            Assert.That(game.Hubs.LapsUntilDeadline, Is.EqualTo(0));
            Assert.That(game.Hubs.LastLossReason, Is.EqualTo(RunLossReason.DeadlineExpired));
            Assert.That(game.Hubs.HubIds.Any(game.Hubs.HasEverSettled), Is.False);
            Assert.That(game.Track.TryDepart(), Is.False);
        }

        /// <summary>생산 데이터를 읽어 네 거점과 자원 식별자가 실제 시스템 상태에서 일치하는지 확인한다.</summary>
        [Test]
        public void ProductionData_AlignsAllSystemsAndHud()
        {
            using var game = new GameSession(Definition(), true);
            CollectionAssert.AreEquivalent(new[] { "hub-1", "hub-2", "hub-3", "hub-4" }, game.Hubs.HubIds);
            CollectionAssert.AreEquivalent(new[] { "coal", "scrap", "water", "grain" }, game.Inventory.Snapshot().Keys);
            CollectionAssert.AreEquivalent(game.Hubs.HubIds, game.Track.Nodes.Where(node => node.IsStation).Select(node => node.HubId));
            Assert.That(game.Hubs.HubIds.Count, Is.EqualTo(4));
        }
    }
}
