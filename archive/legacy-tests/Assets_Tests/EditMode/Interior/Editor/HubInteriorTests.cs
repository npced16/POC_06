using System;
using System.Collections.Generic;
using System.Linq;

using UnityEngine;

using UnityEditor;

using NUnit.Framework;

using IronRing.Bootstrap;
using IronRing.Contracts;

namespace IronRing.Interior.Tests
{
    /// <summary>Story 012 조사 규칙: 횟수 초기화·감소, 시드 결정성, 부지 최소 보장, 묻힌 자원·아이템 획득, 칸 전이표.</summary>
    public sealed class HubInteriorTests
    {
        private const int INVESTIGATIONS = 3;
        private const int MINIMUM_PLOTS = 3;
        private const int SEED = 7;
        private const string FIXTURE = "{\"_rows\":3,\"_columns\":6,\"_investigationsPerDocking\":" + "3" + ",\"_minimumFacilityPlots\":" + "3" + ",\"_seed\":7,"
            + "\"_outcomes\":{\"_empty\":55,\"_item\":20,\"_resource\":15,\"_facilityPlot\":10},"
            + "\"_buriedItems\":[\"lantern\",\"gear\"],"
            + "\"_buriedResources\":[{\"_id\":\"coal\",\"_amount\":2},{\"_id\":\"scrap\",\"_amount\":1}]}";
        private static readonly string[] HUBS = { "hub-1", "hub-2" };
        // 확률표 누적 경계(55/75/90)에 대한 대표 굴림값. 결과 띠를 고르는 것이 목적이므로 숫자 자체가 테스트의 요점이다.
        private const float ROLL_EMPTY = 0.10f;
        private const float ROLL_ITEM = 0.60f;
        private const float ROLL_RESOURCE = 0.80f;
        private const float ROLL_PLOT = 0.95f;

        /// <summary>fixture 데이터와 두 거점으로 내부를 만든다. 인벤토리와 아이템은 생략 가능하다.</summary>
        private static HubInterior Create(IRandomSource random, FakeResources resources = null, IItemInventory items = null)
            => new HubInterior(InteriorData.Parse(FIXTURE), HUBS, resources ?? new FakeResources(), items, random);

        /// <summary>정차를 반복해 hub의 모든 잔해 칸을 조사하고 결과 추적 문자열을 돌려준다.</summary>
        private static List<string> RevealAll(HubInterior interior, string hub)
        {
            var trace = new List<string>();
            while (interior.Cells(hub).Any(cell => cell.State == InteriorCellState.Rubble))
            {
                interior.OnDocked(hub);
                foreach (IInteriorCell cell in interior.Cells(hub).Where(cell => cell.State == InteriorCellState.Rubble).ToList())
                {
                    if (!interior.CanInvestigate(hub, cell.Index)) break;
                    InvestigationResult result = interior.Investigate(hub, cell.Index, out string found);
                    trace.Add($"{hub}:{cell.Index}:{result}:{found}");
                }
            }
            return trace;
        }

        /// <summary>hub에서 굴림이 고정 부지에 걸리지 않는 첫 칸을 조사해 결과를 돌려준다(부지는 생성 시 무작위 배치되므로 건너뛴다).</summary>
        private static InvestigationResult InvestigateNonPlot(HubInterior interior, string hub, out int cellIndex, out string found)
        {
            interior.OnDocked(hub);
            for (int index = interior.Cells(hub).Count - 1; index >= 0; index--)
            {
                if (interior.InvestigationsLeft(hub) == 0) interior.OnDocked(hub);
                InvestigationResult result = interior.Investigate(hub, index, out found);
                if (result != InvestigationResult.FacilityPlot) { cellIndex = index; return result; }
            }
            throw new InvalidOperationException("Every cell was a facility plot.");
        }

        [Test]
        public void Construct_AllCellsStartAsRubbleWithUpperThenLowerLayout()
        {
            HubInterior interior = Create(new FixedRandom(ROLL_EMPTY));

            IReadOnlyList<IInteriorCell> cells = interior.Cells("hub-1");

            Assert.That(cells.Count, Is.EqualTo(36));
            Assert.That(cells.All(cell => cell.State == InteriorCellState.Rubble), Is.True);
            Assert.That(cells.Take(18).All(cell => cell.IsUpper), Is.True);
            Assert.That(cells.Skip(18).All(cell => !cell.IsUpper), Is.True);
            Assert.That(cells.Select(cell => cell.Index), Is.EqualTo(Enumerable.Range(0, 36)));
            Assert.That(cells[7].Row, Is.EqualTo(1));
            Assert.That(cells[7].Column, Is.EqualTo(1));
            Assert.That(cells[35].Row, Is.EqualTo(2));
            Assert.That(cells[35].Column, Is.EqualTo(5));
            Assert.That(interior.HubIds, Is.EqualTo(HUBS));
        }

        [Test]
        public void OnDocked_ResetsInvestigationsToDataValueAndRaisesEvent()
        {
            HubInterior interior = Create(new FixedRandom(ROLL_EMPTY));
            var events = new List<(string hub, int left)>();
            interior.InvestigationsChanged += (hub, left) => events.Add((hub, left));
            Assert.That(interior.InvestigationsLeft("hub-1"), Is.Zero, "No investigations before the first docking.");

            interior.OnDocked("hub-1");

            Assert.That(interior.InvestigationsLeft("hub-1"), Is.EqualTo(INVESTIGATIONS));
            Assert.That(interior.InvestigationsLeft("hub-2"), Is.Zero, "Docking resets only the docked hub.");
            Assert.That(events, Is.EqualTo(new[] { ("hub-1", INVESTIGATIONS) }));
        }

        [Test]
        public void Investigate_DecrementsAndRefusesAtZero()
        {
            HubInterior interior = Create(new FixedRandom(ROLL_EMPTY));
            interior.OnDocked("hub-1");

            for (int i = 0; i < INVESTIGATIONS; i++)
            {
                Assert.That(interior.CanInvestigate("hub-1", i), Is.True);
                interior.Investigate("hub-1", i, out _);
                Assert.That(interior.InvestigationsLeft("hub-1"), Is.EqualTo(INVESTIGATIONS - 1 - i));
            }

            Assert.That(interior.InvestigationsLeft("hub-1"), Is.Zero);
            Assert.That(interior.Cells("hub-1")[INVESTIGATIONS].State, Is.EqualTo(InteriorCellState.Rubble));
            Assert.That(interior.CanInvestigate("hub-1", INVESTIGATIONS), Is.False);
            Assert.Throws<InvalidOperationException>(() => interior.Investigate("hub-1", INVESTIGATIONS, out _));
        }

        [Test]
        public void Redock_RestoresInvestigationsForNextLap()
        {
            HubInterior interior = Create(new FixedRandom(ROLL_EMPTY));
            interior.OnDocked("hub-1");
            for (int i = 0; i < INVESTIGATIONS; i++) interior.Investigate("hub-1", i, out _);
            Assert.That(interior.CanInvestigate("hub-1", INVESTIGATIONS), Is.False);

            interior.OnDocked("hub-1");

            Assert.That(interior.InvestigationsLeft("hub-1"), Is.EqualTo(INVESTIGATIONS));
            Assert.That(interior.CanInvestigate("hub-1", INVESTIGATIONS), Is.True);
        }

        [Test]
        public void SameSeed_ProducesIdenticalLayoutOutcomesAndInventory()
        {
            var firstStock = new FakeResources();
            var secondStock = new FakeResources();
            HubInterior first = Create(new LcgRandom(SEED), firstStock);
            HubInterior second = Create(new LcgRandom(SEED), secondStock);

            var firstTrace = new List<string>();
            var secondTrace = new List<string>();
            foreach (string hub in HUBS) { firstTrace.AddRange(RevealAll(first, hub)); secondTrace.AddRange(RevealAll(second, hub)); }

            Assert.That(firstTrace, Is.Not.Empty);
            Assert.That(secondTrace, Is.EqualTo(firstTrace));
            foreach (string hub in HUBS)
                Assert.That(second.Cells(hub).Select(cell => cell.State), Is.EqualTo(first.Cells(hub).Select(cell => cell.State)));
            Assert.That(secondStock.Snapshot(), Is.EquivalentTo(firstStock.Snapshot()));
        }

        [Test]
        public void EveryHub_RevealsAtLeastMinimumFacilityPlots()
        {
            HubInterior onlyGuaranteed = Create(new FixedRandom(ROLL_EMPTY));
            HubInterior seeded = Create(new LcgRandom(SEED));

            foreach (string hub in HUBS) { RevealAll(onlyGuaranteed, hub); RevealAll(seeded, hub); }

            foreach (string hub in HUBS)
            {
                int guaranteed = onlyGuaranteed.Cells(hub).Count(cell => cell.State == InteriorCellState.FacilityPlot);
                Assert.That(guaranteed, Is.EqualTo(MINIMUM_PLOTS), hub + ": with an empty-only roll the fixed placement alone supplies the minimum.");
                Assert.That(onlyGuaranteed.Cells(hub).Select(cell => cell.Index).Distinct().Count(), Is.EqualTo(36));
                Assert.That(seeded.Cells(hub).Count(cell => cell.State == InteriorCellState.FacilityPlot), Is.GreaterThanOrEqualTo(MINIMUM_PLOTS), hub);
            }
        }

        [Test]
        public void FoundResource_AddsDataAmountToInventoryAndClearsCell()
        {
            var stock = new FakeResources();
            HubInterior interior = Create(new FixedRandom(ROLL_RESOURCE, 0), stock);
            var reports = new List<(string hub, int cell, InvestigationResult result, string found)>();
            interior.Investigated += (hub, cell, result, found) => reports.Add((hub, cell, result, found));

            InvestigationResult outcome = InvestigateNonPlot(interior, "hub-1", out int index, out string foundId);

            Assert.That(outcome, Is.EqualTo(InvestigationResult.FoundResource));
            Assert.That(foundId, Is.EqualTo("coal"));
            Assert.That(stock.Get("coal"), Is.EqualTo(2));
            Assert.That(interior.Cells("hub-1")[index].State, Is.EqualTo(InteriorCellState.Empty));
            Assert.That(reports.Last(), Is.EqualTo(("hub-1", index, InvestigationResult.FoundResource, "coal")));
        }

        [Test]
        public void FoundItem_WithoutItemInventory_ReportsIdAndClearsCell()
        {
            var stock = new FakeResources();
            HubInterior interior = Create(new FixedRandom(ROLL_ITEM, 1), stock, null);

            InvestigationResult outcome = InvestigateNonPlot(interior, "hub-1", out int index, out string foundId);

            Assert.That(outcome, Is.EqualTo(InvestigationResult.FoundItem));
            Assert.That(foundId, Is.EqualTo("gear"));
            Assert.That(interior.Cells("hub-1")[index].State, Is.EqualTo(InteriorCellState.Empty));
            Assert.That(stock.Snapshot(), Is.Empty, "Items never touch the resource stock.");
        }

        [Test]
        public void FoundItem_WithItemInventory_AddsOneItem()
        {
            var items = new FakeItems();
            HubInterior interior = Create(new FixedRandom(ROLL_ITEM, 1), null, items);

            InvestigationResult outcome = InvestigateNonPlot(interior, "hub-1", out _, out string foundId);

            Assert.That(outcome, Is.EqualTo(InvestigationResult.FoundItem));
            Assert.That(items.Count(foundId), Is.EqualTo(1));
        }

        [Test]
        public void FoundItem_AttachedLater_UsesAttachedInventory()
        {
            var items = new FakeItems();
            HubInterior interior = Create(new FixedRandom(ROLL_ITEM, 0));
            interior.AttachItemInventory(items);

            InvestigateNonPlot(interior, "hub-1", out _, out string foundId);

            Assert.That(foundId, Is.EqualTo("lantern"));
            Assert.That(items.Count("lantern"), Is.EqualTo(1));
        }

        [Test]
        public void Roll_SelectsOutcomeBandsAndPlotRevealsFacilityPlot()
        {
            HubInterior empty = Create(new FixedRandom(ROLL_EMPTY));
            HubInterior plot = Create(new FixedRandom(ROLL_PLOT));

            Assert.That(InvestigateNonPlot(empty, "hub-1", out int emptyIndex, out string none), Is.EqualTo(InvestigationResult.Empty));
            Assert.That(none, Is.Null);
            Assert.That(empty.Cells("hub-1")[emptyIndex].State, Is.EqualTo(InteriorCellState.Empty));
            plot.OnDocked("hub-1");
            Assert.That(plot.Investigate("hub-1", 0, out _), Is.EqualTo(InvestigationResult.FacilityPlot));
            Assert.That(plot.Cells("hub-1")[0].State, Is.EqualTo(InteriorCellState.FacilityPlot));
        }

        [Test]
        public void Investigate_RevealedCell_IsRefusedAndRaisesCellChangedOnce()
        {
            HubInterior interior = Create(new FixedRandom(ROLL_EMPTY));
            var changed = new List<(string hub, int cell)>();
            interior.CellChanged += (hub, cell) => changed.Add((hub, cell));
            interior.OnDocked("hub-1");

            interior.Investigate("hub-1", 4, out _);

            Assert.That(changed, Is.EqualTo(new[] { ("hub-1", 4) }));
            Assert.That(interior.CanInvestigate("hub-1", 4), Is.False);
            Assert.Throws<InvalidOperationException>(() => interior.Investigate("hub-1", 4, out _));
            Assert.That(interior.InvestigationsLeft("hub-1"), Is.EqualTo(INVESTIGATIONS - 1), "A refused investigation costs nothing.");
        }

        [Test]
        public void CellTransitions_FollowTheTable()
        {
            // Arrange: 모든 칸을 드러내면(빈 터 굴림) 고정 부지 3칸과 빈 터 33칸이 남고, 잔해는 손대지 않은 두 번째 거점에서 확인한다.
            HubInterior interior = Create(new FixedRandom(ROLL_EMPTY));
            RevealAll(interior, "hub-1");
            int emptyIndex = interior.Cells("hub-1").First(cell => cell.State == InteriorCellState.Empty).Index;
            int plotIndex = interior.Cells("hub-1").First(cell => cell.State == InteriorCellState.FacilityPlot).Index;

            Assert.That(interior.TryPlaceItem("hub-1", emptyIndex, "lantern"), Is.True);
            Assert.That(interior.Cells("hub-1")[emptyIndex].State, Is.EqualTo(InteriorCellState.ItemPlaced));
            Assert.That(interior.Cells("hub-1")[emptyIndex].ItemId, Is.EqualTo("lantern"));
            Assert.That(interior.TryPlaceItem("hub-1", emptyIndex, "gear"), Is.False, "ItemPlaced refuses a second item.");
            Assert.That(interior.TryBuildFacility("hub-1", emptyIndex, "wall"), Is.False, "Facilities need a plot.");
            Assert.That(interior.TryRemoveItem("hub-1", emptyIndex), Is.True);
            Assert.That(interior.Cells("hub-1")[emptyIndex].State, Is.EqualTo(InteriorCellState.Empty));
            Assert.That(interior.Cells("hub-1")[emptyIndex].ItemId, Is.Null);
            Assert.That(interior.TryRemoveItem("hub-1", emptyIndex), Is.False);
            Assert.That(interior.TryPlaceItem("hub-1", plotIndex, "lantern"), Is.False, "Plots refuse items.");
            Assert.That(interior.TryBuildFacility("hub-1", plotIndex, "wall"), Is.True);
            Assert.That(interior.Cells("hub-1")[plotIndex].State, Is.EqualTo(InteriorCellState.FacilityBuilt));
            Assert.That(interior.Cells("hub-1")[plotIndex].FacilityId, Is.EqualTo("wall"));
            Assert.That(interior.TryBuildFacility("hub-1", plotIndex, "turret"), Is.False);
            IInteriorFacilityCells facilityCells = interior;
            int secondPlot = interior.Cells("hub-1").First(cell => cell.State == InteriorCellState.FacilityPlot).Index;
            Assert.That(facilityCells.TryBuildFacility("hub-1", secondPlot, " "), Is.False, "Blank facility id is refused, not thrown.");
            Assert.That(interior.Cells("hub-1")[secondPlot].State, Is.EqualTo(InteriorCellState.FacilityPlot));
            int rubble = interior.Cells("hub-2").First(cell => cell.State == InteriorCellState.Rubble).Index;
            Assert.That(interior.TryPlaceItem("hub-2", rubble, "lantern"), Is.False, "Rubble must be investigated first.");
            Assert.That(interior.TryBuildFacility("hub-2", rubble, "wall"), Is.False);
            Assert.That(interior.Cells("hub-2")[rubble].State, Is.EqualTo(InteriorCellState.Rubble));
        }

        [Test]
        public void UnknownHubOrCell_IsRejected()
        {
            HubInterior interior = Create(new FixedRandom(ROLL_EMPTY));

            Assert.Throws<ArgumentException>(() => interior.Cells("hub-9"));
            Assert.Throws<ArgumentException>(() => interior.OnDocked("hub-9"));
            Assert.That(interior.CanInvestigate("hub-9", 0), Is.False);
            interior.OnDocked("hub-1");
            Assert.That(interior.CanInvestigate("hub-1", -1), Is.False);
            Assert.That(interior.CanInvestigate("hub-1", 36), Is.False);
            Assert.Throws<ArgumentException>(() => new HubInterior(InteriorData.Parse(FIXTURE), new[] { "hub-1", "hub-1" }, new FakeResources(), null, new FixedRandom()));
        }

        [Test]
        public void GameSession_DocksInteriorAtStartAndOnEveryHubArrival()
        {
            GameDefinition data = GameDefinition.Parse(Read("Assets/Scripts/Track/Resources/Track/ring-track.json"),
                Read("Assets/Scripts/Habitat/Resources/Story002/habitats.json"), Read("Assets/Scripts/Npc/Data/NpcPool.json"),
                Read("Assets/Scripts/Hub/Data/HubProfiles.json"), Read("Assets/Scripts/Combat/Resources/Story005Encounters.json"),
                Read("Assets/Scripts/Defense/Data/Facilities.json"), Read("Assets/Scripts/Bootstrap/Data/MainSettings.json"),
                Read("Assets/Scripts/Interior/Resources/Interior/HubInterior.json"));
            using var game = new GameSession(data, true);
            string start = game.Track.CurrentNode.HubId;
            Assert.That(game.Interior, Is.Not.Null);
            Assert.That(game.Interior.HubIds, Is.EquivalentTo(game.Hubs.HubIds));
            Assert.That(game.Interior.InvestigationsLeft(start), Is.EqualTo(data.Interior.InvestigationsPerDocking), "The starting hub counts as a docking.");
            while (game.Interior.InvestigationsLeft(start) > 0)
                game.Interior.Investigate(start, game.Interior.Cells(start).First(cell => cell.State == InteriorCellState.Rubble).Index, out _);
            Assert.That(game.Interior.InvestigationsLeft(start), Is.Zero);

            string next;
            do
            {
                Assert.That(game.Track.TryDepart(), Is.True);
                game.Track.Arrive();
                while (game.Waypoints.IsEncounterActive) game.Waypoints.Advance();
                next = game.Track.CurrentNode.HubId;
            } while (next == null);

            Assert.That(next, Is.Not.EqualTo(start));
            Assert.That(game.Interior.InvestigationsLeft(next), Is.EqualTo(data.Interior.InvestigationsPerDocking));
            do
            {
                Assert.That(game.Track.TryDepart(), Is.True);
                game.Track.Arrive();
                while (game.Waypoints.IsEncounterActive) game.Waypoints.Advance();
            } while (game.Track.CurrentNode.HubId != start);
            Assert.That(game.Interior.InvestigationsLeft(start), Is.EqualTo(data.Interior.InvestigationsPerDocking), "Docking again on the next lap resets the count.");
        }

        /// <summary>path의 프로젝트 TextAsset을 읽어 JSON 문자열을 돌려준다.</summary>
        private static string Read(string path) => AssetDatabase.LoadAssetAtPath<TextAsset>(path).text;

        /// <summary>고정 굴림과 고정 선택 index를 돌려주는 결정적 난수. 부분 셔플에도 종료한다.</summary>
        private sealed class FixedRandom : IRandomSource
        {
            private readonly float _roll;
            private readonly int _index;
            public FixedRandom(float roll = 0f, int index = 0) { _roll = roll; _index = index; }
            public float NextFloat() => _roll;
            public int NextInt(int minInclusive, int maxExclusive) => Math.Min(minInclusive + _index, maxExclusive - 1);
        }

        /// <summary>시드 기반 LCG. 같은 시드면 같은 수열을 낸다(테스트 전용, 런타임 SeededRandom과 독립).</summary>
        private sealed class LcgRandom : IRandomSource
        {
            private uint _state;
            public LcgRandom(int seed) { _state = unchecked((uint)seed); }
            public float NextFloat() { _state = unchecked(_state * 1664525u + 1013904223u); return (_state >> 8) / 16777216f; }
            public int NextInt(int minInclusive, int maxExclusive) => minInclusive + (int)(NextFloat() * (maxExclusive - minInclusive));
        }

        /// <summary>자원 재고 페이크: Add만 기록하고 지출은 거부한다.</summary>
        private sealed class FakeResources : IResourceInventory
        {
            private readonly Dictionary<string, int> _stock = new Dictionary<string, int>(StringComparer.Ordinal);
            public event Action<string, int> Changed;
            public int Get(string resourceId) => _stock.TryGetValue(resourceId, out int value) ? value : 0;
            public void Add(string resourceId, int amount) { _stock[resourceId] = Get(resourceId) + amount; Changed?.Invoke(resourceId, _stock[resourceId]); }
            public bool TrySpend(string resourceId, int amount) => false;
            public IReadOnlyDictionary<string, int> Snapshot() => new Dictionary<string, int>(_stock);
        }

        /// <summary>아이템 인벤토리 페이크(스토리 013 자리): 수량만 기록한다.</summary>
        private sealed class FakeItems : IItemInventory
        {
            private readonly Dictionary<string, int> _items = new Dictionary<string, int>(StringComparer.Ordinal);
            public event Action<string, int> Changed;
            public int Count(string itemId) => _items.TryGetValue(itemId, out int value) ? value : 0;
            public void Add(string itemId, int amount) { _items[itemId] = Count(itemId) + amount; Changed?.Invoke(itemId, _items[itemId]); }
            public bool TryRemove(string itemId, int amount) => false;
            public IReadOnlyDictionary<string, int> Snapshot() => new Dictionary<string, int>(_items);
            public bool CanCraft(string itemId) => false;
            public bool TryCraft(string itemId) => false;
        }
    }
}
