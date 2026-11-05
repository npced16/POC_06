using UnityEngine;

namespace IronRing.Save
{
    /// <summary>스펙 003 FR-009. 백그라운드 실행 켜기·끄기를 PlayerPrefs에 기억하고 Application.runInBackground에 적용한다.</summary>
    public static class BackgroundRunSetting
    {
        private const string PREF_KEY = "settings_run_in_background";

        /// <summary>지금 백그라운드 실행이 켜져 있는지. 저장값이 없으면 켜짐.</summary>
        public static bool IsOn => PlayerPrefs.GetInt(PREF_KEY, 1) == 1;

        /// <summary>게임 시작 때 저장값을 Application.runInBackground에 적용한다.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Apply()
        {
            Application.runInBackground = IsOn;
        }

        /// <summary>값을 뒤집어 PlayerPrefs에 저장하고 적용한다. 바뀐 값을 돌려준다.</summary>
        public static bool Toggle()
        {
            bool next = !IsOn;
            PlayerPrefs.SetInt(PREF_KEY, next ? 1 : 0);
            PlayerPrefs.Save();
            Application.runInBackground = next;
            return next;
        }
    }
}
