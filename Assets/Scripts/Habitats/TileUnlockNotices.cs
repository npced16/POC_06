using System;
using System.Collections.Generic;

using IronRing.Contracts;
using IronRing.Hud.Ui;
using IronRing.Localization;

namespace IronRing.Habitats
{
    /// <summary>
    /// 스토리 050 로직 알림. HabitatCodex의 TileUnlocked·RecipesLearned를 받아 로그("UNLOCK: tile …", "UNLOCK: recipe …")와
    /// 토스트 한 줄("새 타일: 증기관 · 굴뚝", "새 설계도: 배관 회랑 · 톱니공방", ko.json unlock_toast_*)을 낸다.
    /// 이름: 타일은 아이템 카탈로그, 건물은 서식지 카탈로그. 화면 배치(핫바 반짝임·도감)는 HUD가 같은 이벤트로 따로 한다.
    /// </summary>
    public sealed class TileUnlockNotices : IDisposable
    {
        private readonly HabitatCodex _codex;
        private readonly Func<string, string> _tileName;
        private readonly Func<string, string> _recipeName;

        /// <summary>tileName·recipeName은 id → 표시 이름(없으면 id).</summary>
        public TileUnlockNotices(HabitatCodex codex, Func<string, string> tileName, Func<string, string> recipeName)
        {
            _codex = codex ?? throw new ArgumentNullException(nameof(codex));
            _tileName = tileName;
            _recipeName = recipeName;
            _codex.TileUnlocked += OnTileUnlocked;
            _codex.RecipesLearned += OnRecipesLearned;
        }

        private void OnTileUnlocked(IReadOnlyList<string> tiles, string byRecipe)
        {
            UnityEngine.Debug.Log($"UNLOCK: tile {string.Join(",", tiles)} by {byRecipe}");
            Toast("unlock_toast_tiles", tiles, _tileName);
        }

        private void OnRecipesLearned(IReadOnlyList<string> recipes, string byRecipe)
        {
            UnityEngine.Debug.Log($"UNLOCK: recipe {string.Join(",", recipes)} by {byRecipe}");
            Toast("unlock_toast_recipes", recipes, _recipeName);
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
            _codex.RecipesLearned -= OnRecipesLearned;
        }
    }
}
