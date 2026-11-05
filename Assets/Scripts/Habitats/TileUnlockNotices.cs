using System;
using System.Collections.Generic;

using IronRing.Contracts;
using IronRing.Hud.Ui;
using IronRing.Localization;

namespace IronRing.Habitats
{
    /// <summary>
    /// 건물 첫 완성으로 열린 타일을 받아 로그와 "새 타일" 토스트를 낸다.
    /// 이름: 타일은 아이템 카탈로그, 건물은 서식지 카탈로그. 화면 배치(핫바 반짝임·도감)는 HUD가 같은 이벤트로 따로 한다.
    /// </summary>
    public sealed class TileUnlockNotices : IDisposable
    {
        private readonly HabitatCodex _codex;
        private readonly Func<string, string> _tileName;

        /// <summary>codex의 타일 해금 이벤트를 구독한다. tileName은 타일 id의 표시 이름을 반환한다.</summary>
        public TileUnlockNotices(HabitatCodex codex, Func<string, string> tileName)
        {
            _codex = codex ?? throw new ArgumentNullException(nameof(codex));
            _tileName = tileName;
            _codex.TileUnlocked += OnTileUnlocked;
        }

        private void OnTileUnlocked(IReadOnlyList<string> tiles, string byRecipe)
        {
            UnityEngine.Debug.Log($"UNLOCK: tile {string.Join(",", tiles)} by {byRecipe}");
        }

        private void Toast(string key, IReadOnlyList<string> ids, Func<string, string> nameOf)
        {
            var names = new List<string>(ids.Count);
            foreach (string id in ids) names.Add(nameOf?.Invoke(id) ?? id);
            string text = string.Format(Text(key, "{0}"), string.Join(Text("unlock_name_sep", " · "), names));
            ToastScreen.Show(text, _codex.Settings.ToastSeconds);
        }

        private static string Text(string key, string fallback)
        {
            try { return Strings.Get(key); }
            catch (KeyNotFoundException) { return fallback; }
        }

        /// <summary>구독을 푼다.</summary>
        public void Dispose()
        {
            _codex.TileUnlocked -= OnTileUnlocked;
        }
    }
}
