using System.Collections.Generic;
using System.IO;

using NUnit.Framework;

using IronRing.Contracts;
using IronRing.Defense.Demo;

namespace IronRing.Defense.Tests
{
    public sealed class FacilityBuilderTests
    {
        private DefenseDemoInventory _inventory;
        private DefenseDemoHubs _hubs;
        private FacilityCatalog _catalog;
        private FacilityBuilder _builder;
        private bool _docked;

        /// <summary>Loads the production catalog and creates isolated interface fakes before each test.</summary>
        [SetUp]
        public void SetUp()
        {
            _catalog = FacilityCatalog.Parse(File.ReadAllText("Assets/Scripts/Defense/Data/Facilities.json"));
            _inventory = new DefenseDemoInventory();
            _inventory.Add("scrap", 30); _inventory.Add("coal", 10); _inventory.Add("grain", 10);
            _hubs = new DefenseDemoHubs("a", "b");
            _docked = true;
            _builder = Create(_catalog.AllowBuildInRuins, _catalog.PerHubLimit);
        }

        /// <summary>Constructs a builder from fixture dependencies and supplied rule overrides.</summary>
        private FacilityBuilder Create(bool ruins, int limit) => new FacilityBuilder(_catalog.CreateDefinitions(), limit, ruins, _inventory, _hubs, id => _docked);

        /// <summary>Verifies all three production definitions, prices and prosperity metadata are read from JSON.</summary>
        [Test]
        public void CatalogContainsProvisionalData()
        {
            var definitions = _catalog.CreateDefinitions();
            Assert.That(definitions.Count, Is.EqualTo(3));
            Assert.That(definitions[0].Costs["scrap"], Is.EqualTo(3));
            Assert.That(definitions[1].Costs["coal"], Is.EqualTo(1));
            Assert.That(definitions[2].Costs["grain"], Is.EqualTo(2));
            Assert.That(definitions[2].IsProsperityFacility, Is.True);
            Assert.That(_catalog.PerHubLimit, Is.EqualTo(3));
            Assert.That(_catalog.AllowBuildInRuins, Is.False);
        }

        /// <summary>Builds all types through the contract and checks exact costs, hub isolation, effects and one event per build.</summary>
        [Test]
        public void BuildsSpendApplyEffectsAndPublishCommittedState()
        {
            IFacilityBuilder contract = _builder;
            int events = 0;
            contract.FacilityBuilt += (hub, id) =>
            {
                events++;
                Assert.That(contract.FacilitiesAt(hub), Does.Contain(id));
                Assert.That(contract.CanBuild(hub, "wall"), Is.EqualTo(events < _catalog.PerHubLimit));
            };
            Assert.That(contract.TryBuild("a", "wall"), Is.True);
            Assert.That(contract.TryBuild("a", "turret"), Is.True);
            Assert.That(contract.TryBuild("a", "workshop"), Is.True);
            Assert.That(_inventory.Get("scrap"), Is.EqualTo(20));
            Assert.That(_inventory.Get("coal"), Is.EqualTo(9));
            Assert.That(_inventory.Get("grain"), Is.EqualTo(8));
            Assert.That(contract.CombatBonus("a"), Is.EqualTo(8f));
            Assert.That(_hubs.GetProsperity("a"), Is.EqualTo(2f));
            Assert.That(contract.CombatBonus("b"), Is.Zero);
            Assert.That(_hubs.GetProsperity("b"), Is.Zero);
            Assert.That(events, Is.EqualTo(3));
        }

        /// <summary>Removes a secondary resource and verifies refusal causes no inventory, hub or event mutation.</summary>
        [Test]
        public void InsufficientSecondaryResourceChangesNothing()
        {
            _inventory.TrySpend("coal", 10);
            int events = 0;
            _inventory.Changed += (id, count) => events++;
            _builder.FacilityBuilt += (hub, id) => events++;
            Assert.That(_builder.CanBuild("a", "turret"), Is.False);
            Assert.That(_builder.TryBuild("a", "turret"), Is.False);
            Assert.That(_inventory.Get("scrap"), Is.EqualTo(30));
            Assert.That(_builder.FacilitiesAt("a"), Is.Empty);
            Assert.That(_hubs.GetProsperity("a"), Is.Zero);
            Assert.That(events, Is.Zero);
        }

        /// <summary>Fills one hub and verifies capacity refusal preserves resources while another hub retains capacity.</summary>
        [Test]
        public void LimitIsPerHubAndRefusalDoesNotSpend()
        {
            for (int i = 0; i < _catalog.PerHubLimit; i++) Assert.That(_builder.TryBuild("a", "wall"), Is.True);
            int before = _inventory.Get("scrap");
            Assert.That(_builder.TryBuild("a", "wall"), Is.False);
            Assert.That(_inventory.Get("scrap"), Is.EqualTo(before));
            Assert.That(_builder.TryBuild("b", "wall"), Is.True);
        }

        /// <summary>Checks both configured ruin policies using a ruined fixture hub.</summary>
        [TestCase(false)]
        [TestCase(true)]
        public void RuinPolicyIsDataDriven(bool allow)
        {
            _hubs.Ruin("a");
            var builder = Create(allow, 3);
            Assert.That(builder.TryBuild("a", "wall"), Is.EqualTo(allow));
            Assert.That(_inventory.Get("scrap"), Is.EqualTo(allow ? 27 : 30));
        }

        /// <summary>Rejects undocked and unknown inputs without changing facility state.</summary>
        [Test]
        public void DockingAndKnownIdsAreRequired()
        {
            _docked = false;
            Assert.That(_builder.TryBuild("a", "wall"), Is.False);
            _docked = true;
            Assert.That(_builder.TryBuild("missing", "wall"), Is.False);
            Assert.That(_builder.TryBuild("a", "missing"), Is.False);
            Assert.That(_builder.TryBuild(null, "wall"), Is.False);
            Assert.That(_inventory.Get("scrap"), Is.EqualTo(30));
        }

        /// <summary>Checks read-only queries, snapshot isolation and protection against inventory callback reentry.</summary>
        [Test]
        public void QueriesDoNotMutateAndCallbacksCannotReenterBuild()
        {
            var before = _builder.FacilitiesAt("a");
            Assert.That(_builder.CanBuild("a", "wall"), Is.True);
            Assert.That(_inventory.Get("scrap"), Is.EqualTo(30));
            _inventory.Changed += (id, amount) => Assert.That(_builder.TryBuild("a", "wall"), Is.False);
            Assert.That(_builder.TryBuild("a", "wall"), Is.True);
            Assert.That(before, Is.Empty);
            Assert.That(_builder.FacilitiesAt("a").Count, Is.EqualTo(1));
        }

        /// <summary>Uses custom data values and verifies effects and capacity follow configuration rather than defaults.</summary>
        [Test]
        public void CustomDefinitionsControlEffectsAndLimit()
        {
            var definition = new FacilityDefinition("custom", new Dictionary<string, int> { { "grain", 7 } }, 13f, 4f, true);
            var builder = new FacilityBuilder(new[] { definition }, 1, false, _inventory, _hubs, id => true);
            Assert.That(builder.TryBuild("b", "custom"), Is.True);
            Assert.That(builder.CombatBonus("b"), Is.EqualTo(13));
            Assert.That(_hubs.GetProsperity("b"), Is.EqualTo(4));
            Assert.That(_inventory.Get("grain"), Is.EqualTo(3));
            Assert.That(builder.TryBuild("b", "custom"), Is.False);
        }
    }
}
