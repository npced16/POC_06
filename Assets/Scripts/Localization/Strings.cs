using System;
using System.Collections.Generic;

using UnityEngine;

namespace IronRing.Localization
{
    /// <summary>Resources의 한국어 테이블을 읽는 런타임 문자열 API.</summary>
    public static class Strings
    {
        private static Dictionary<string, string> _values;

        /// <summary>영어 snake_case key에 대응하는 한국어를 반환하며 누락 키는 예외로 알린다.</summary>
        public static string Get(string key)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));
            if (_values == null)
            {
                TextAsset asset = Resources.Load<TextAsset>("ko");
                if (asset == null) throw new InvalidOperationException("Missing localization resource: ko");
                _values = Parse(asset.text);
            }
            if (!_values.TryGetValue(key, out string value)) throw new KeyNotFoundException(key);
            return value;
        }

        /// <summary>json 테이블을 검증하고 키와 한국어 값의 새 사전을 반환한다.</summary>
        public static Dictionary<string, string> Parse(string json)
        {
            Table table = JsonUtility.FromJson<Table>(json);
            if (table == null || table.Entries == null) throw new FormatException("Missing entries");
            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (Entry entry in table.Entries)
            {
                if (string.IsNullOrWhiteSpace(entry.Key) || string.IsNullOrWhiteSpace(entry.Value))
                    throw new FormatException("Empty localization entry");
                if (!System.Text.RegularExpressions.Regex.IsMatch(entry.Key, "^[a-z][a-z0-9]*(_[a-z0-9]+)*$"))
                    throw new FormatException("Invalid localization key: " + entry.Key);
                if (values.ContainsKey(entry.Key)) throw new FormatException("Duplicate localization key: " + entry.Key);
                values.Add(entry.Key, entry.Value);
            }
            return values;
        }

        [Serializable]
        private sealed class Table
        {
            [SerializeField] private Entry[] _entries;
            public Entry[] Entries => _entries;
        }

        [Serializable]
        private sealed class Entry
        {
            [SerializeField] private string _key;
            [SerializeField] private string _value;
            public string Key => _key;
            public string Value => _value;
        }
    }
}
