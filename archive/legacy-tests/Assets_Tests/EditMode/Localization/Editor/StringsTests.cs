using System;
using System.Collections.Generic;

using UnityEngine;

using NUnit.Framework;

namespace IronRing.Localization.Tests
{
    public sealed class StringsTests
    {
        /// <summary>대표 키를 조회하여 한국어 명칭이 정확히 반환되는지 확인한다.</summary>
        [TestCase("hub_1", "주조소")]
        [TestCase("hub_2", "정원")]
        [TestCase("hub_3", "항구")]
        [TestCase("hub_4", "성채")]
        [TestCase("waypoint_north", "북쪽 고개")]
        [TestCase("resource_coal", "석탄")]
        [TestCase("facility_turret", "포탑")]
        public void GetReturnsKorean(string key, string expected)
        {
            Assert.That(Strings.Get(key), Is.EqualTo(expected));
        }

        /// <summary>필수 범주의 키를 조회하여 누락과 공백 없이 한국어를 반환하는지 확인한다.</summary>
        [Test]
        public void RequiredCategoriesExist()
        {
            string[] keys = { "waypoint_east", "waypoint_south", "waypoint_west", "resource_scrap", "resource_water", "resource_grain", "facility_wall", "facility_workshop", "event_supply", "combat_start", "combat_victory", "hud_resources", "hud_location", "hud_prosperity", "defeat_title", "defeat_body" };
            foreach (string key in keys) Assert.That(Strings.Get(key), Does.Match("[가-힣]"), key);
            var values = Strings.Parse(Resources.Load<TextAsset>("ko").text);
            foreach (var pair in values) Assert.That(pair.Value, Does.Match("[가-힣]"), pair.Key);
        }

        /// <summary>존재하지 않는 키와 null 입력을 조회하여 명확한 예외를 확인한다.</summary>
        [Test]
        public void MissingKeysFailExplicitly()
        {
            Assert.Throws<KeyNotFoundException>(() => Strings.Get("missing_key"));
            Assert.Throws<ArgumentNullException>(() => Strings.Get(null));
        }

        /// <summary>중복 키와 잘못된 키 및 공백 값을 파싱하여 잘못된 테이블이 거부되는지 확인한다.</summary>
        [TestCase("{\"_entries\":[{\"_key\":\"a\",\"_value\":\"값\"},{\"_key\":\"a\",\"_value\":\"값\"}]}")]
        [TestCase("{\"_entries\":[{\"_key\":\"Bad-Key\",\"_value\":\"값\"}]}")]
        [TestCase("{\"_entries\":[{\"_key\":\"a\",\"_value\":\" \"}]}")]
        public void InvalidTablesAreRejected(string json)
        {
            Assert.Throws<FormatException>(() => Strings.Parse(json));
        }
    }
}
