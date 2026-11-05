using System;
using System.Collections.Generic;

using UnityEngine;

namespace IronRing.Combat
{
    /// <summary>전투에서 획득한 설계도 단서 id를 POC 저장소에 보관해 중복 보상을 판정한다.</summary>
    public static class BattleHintJournal
    {
        private const string SAVE_KEY = "IronRing.BattleHints.v1";

        /// <summary>저장된 단서 id를 읽어 현재까지 얻은 설계도 단서 목록을 반환한다.</summary>
        public static IReadOnlyList<string> All()
        {
            string raw = PlayerPrefs.GetString(SAVE_KEY, "");
            return string.IsNullOrEmpty(raw) ? Array.Empty<string>() : raw.Split('|');
        }

        /// <summary>typeId 단서를 이전에 획득했는지 저장 목록에서 찾아 반환한다.</summary>
        public static bool Has(string typeId)
        {
            foreach (string id in All()) if (id == typeId) return true;
            return false;
        }

        /// <summary>새 typeId를 저장하고 true를 반환하며 이미 있거나 잘못된 id면 false를 반환한다.</summary>
        public static bool Record(string typeId)
        {
            if (string.IsNullOrWhiteSpace(typeId) || typeId.Contains("|") || Has(typeId)) return false;
            var ids = new List<string>(All()) { typeId };
            PlayerPrefs.SetString(SAVE_KEY, string.Join("|", ids));
            PlayerPrefs.Save();
            return true;
        }
    }
}
