using System;

using UnityEngine;

using UnityEditor;

using NUnit.Framework;

namespace IronRing.Interior.Tests
{
    /// <summary>Story 012 내부 데이터 파싱·검증과 제작 데이터가 브리프(3×6 두 구역, 정차당 3회, 부지 최소 3칸)를 따르는지.</summary>
    public sealed class InteriorDataTests
    {
        private const string VALID = "{\"_rows\":2,\"_columns\":4,\"_investigationsPerDocking\":1,\"_minimumFacilityPlots\":2,\"_seed\":3,"
            + "\"_outcomes\":{\"_empty\":1,\"_item\":2,\"_resource\":3,\"_facilityPlot\":4},"
            + "\"_buriedItems\":[\"lantern\"],\"_buriedResources\":[{\"_id\":\"coal\",\"_amount\":5}]}";

        [Test]
        public void Parse_ReadsGridRulesAndTables()
        {
            InteriorData data = InteriorData.Parse(VALID);

            Assert.That(data.Rows, Is.EqualTo(2));
            Assert.That(data.Columns, Is.EqualTo(4));
            Assert.That(data.AreaCellCount, Is.EqualTo(8));
            Assert.That(data.CellCount, Is.EqualTo(16));
            Assert.That(data.InvestigationsPerDocking, Is.EqualTo(1));
            Assert.That(data.MinimumFacilityPlots, Is.EqualTo(2));
            Assert.That(data.Seed, Is.EqualTo(3));
            Assert.That(data.Outcomes.Total, Is.EqualTo(10));
            Assert.That(data.Outcomes.FacilityPlot, Is.EqualTo(4));
            Assert.That(data.BuriedItems, Is.EqualTo(new[] { "lantern" }));
            Assert.That(data.BuriedResources[0].Id, Is.EqualTo("coal"));
            Assert.That(data.BuriedResources[0].Amount, Is.EqualTo(5));
        }

        [TestCase("{\"_rows\":0,\"_columns\":4,\"_investigationsPerDocking\":1,\"_minimumFacilityPlots\":0,\"_outcomes\":{\"_empty\":1}}", TestName = "Parse_RejectsZeroRows")]
        [TestCase("{\"_rows\":2,\"_columns\":4,\"_investigationsPerDocking\":1,\"_minimumFacilityPlots\":17,\"_outcomes\":{\"_empty\":1}}", TestName = "Parse_RejectsMorePlotsThanCells")]
        [TestCase("{\"_rows\":2,\"_columns\":4,\"_investigationsPerDocking\":1,\"_minimumFacilityPlots\":0,\"_outcomes\":{\"_empty\":0,\"_item\":0,\"_resource\":0,\"_facilityPlot\":0}}", TestName = "Parse_RejectsZeroTotalWeight")]
        [TestCase("{\"_rows\":2,\"_columns\":4,\"_investigationsPerDocking\":1,\"_minimumFacilityPlots\":0,\"_outcomes\":{\"_empty\":1,\"_item\":-1}}", TestName = "Parse_RejectsNegativeWeight")]
        [TestCase("{\"_rows\":2,\"_columns\":4,\"_investigationsPerDocking\":1,\"_minimumFacilityPlots\":0,\"_outcomes\":{\"_empty\":1,\"_item\":1}}", TestName = "Parse_RejectsItemWeightWithoutItems")]
        [TestCase("{\"_rows\":2,\"_columns\":4,\"_investigationsPerDocking\":1,\"_minimumFacilityPlots\":0,\"_outcomes\":{\"_empty\":1,\"_resource\":1},\"_buriedResources\":[{\"_id\":\"coal\",\"_amount\":0}]}", TestName = "Parse_RejectsZeroResourceAmount")]
        [TestCase("{\"_rows\":2,\"_columns\":4,\"_investigationsPerDocking\":-1,\"_minimumFacilityPlots\":0,\"_outcomes\":{\"_empty\":1}}", TestName = "Parse_RejectsNegativeInvestigations")]
        [TestCase("", TestName = "Parse_RejectsEmptyText")]
        public void Parse_RejectsInvalidData(string json)
        {
            Assert.Throws<ArgumentException>(() => InteriorData.Parse(json));
        }

        [Test]
        public void ProductionData_MatchesBriefAndIsConsistent()
        {
            TextAsset asset = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Scripts/Interior/Resources/Interior/HubInterior.json");
            Assert.That(asset, Is.Not.Null, "Interior data must live under Resources so MainRoot can load it without a scene edit.");

            InteriorData data = InteriorData.Parse(asset.text);

            Assert.That(data.Rows, Is.EqualTo(3), "Brief: 위·아래 각 3×6칸");
            Assert.That(data.Columns, Is.EqualTo(6));
            Assert.That(data.CellCount, Is.EqualTo(36));
            Assert.That(data.InvestigationsPerDocking, Is.EqualTo(3), "Story 012: 정차당 조사 횟수 기본 3");
            Assert.That(data.MinimumFacilityPlots, Is.EqualTo(3), "Story 012: 거점마다 시설 부지 최소 3칸");
            Assert.That(data.Outcomes.IsValid, Is.True);
            Assert.That(data.BuriedItems, Is.Not.Empty);
            Assert.That(data.BuriedResources, Is.Not.Empty);
        }
    }
}
