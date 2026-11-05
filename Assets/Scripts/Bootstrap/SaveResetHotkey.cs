using System;
using System.Reflection;

using UnityEngine;
using UnityEngine.InputSystem;

using IronRing.Combat;
using IronRing.Hud;
using IronRing.Npc;

namespace IronRing.Bootstrap
{
    /// <summary>
    /// F9를 누르면 PlayerPrefs에 남은 POC 저장값(열차 포탑 장비, 설계도 단서, 승객 발견 기록)을 지우고 런을 처음부터 다시 시작한다.
    /// 기존 저장 클래스는 고치지 않고, 저장 키와 메모리 캐시는 리플렉션으로 읽고 비운다.
    /// </summary>
    public sealed class SaveResetHotkey : MonoBehaviour
    {
        private const BindingFlags PRIVATE_STATIC = BindingFlags.Static | BindingFlags.NonPublic;

        private static SaveResetHotkey _instance;

        /// <summary>
        /// 플레이 시작 때 씬과 무관하게 살아 있는 단축키 오브젝트를 하나 만든다.
        /// _instance를 사용하며, 이미 있으면 새로 만들지 않는다.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (_instance != null) return;
            GameObject host = new GameObject("Save Reset Hotkey (F9)");
            DontDestroyOnLoad(host);
            _instance = host.AddComponent<SaveResetHotkey>();
        }

        void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.f9Key.wasPressedThisFrame) return;
            ResetSaves();
        }

        void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        /// <summary>
        /// 세 저장 키를 지우고 열차 장비 캐시를 비운 뒤 활성 씬을 다시 시작한다.
        /// 각 저장 클래스의 키 상수를 사용하며, PlayerPrefs와 TrainArmament의 메모리 상태를 변경한다.
        /// </summary>
        private void ResetSaves()
        {
            DeleteSaveKey(typeof(TrainArmament), "SAVE_KEY");
            DeleteSaveKey(typeof(BattleHintJournal), "SAVE_KEY");
            DeleteSaveKey(typeof(PassengerDiscovery), "PREF_KEY");
            PlayerPrefs.Save();
            // TrainArmament는 처음 읽은 값을 static에 두므로 비워야 기본 포탑부터 다시 읽는다.
            // PassengerDiscovery 캐시는 Restart가 부르는 SubsystemRegistration 훅이 비운다.
            ClearStaticField(typeof(TrainArmament), "_data");
            Debug.Log("SAVE RESET: PlayerPrefs saves cleared by F9, restarting run");
            RunResultScreen.Restart();
        }

        /// <summary>
        /// owner 클래스의 private 문자열 상수 fieldName을 읽어 그 PlayerPrefs 키를 지운다.
        /// owner와 fieldName을 사용하며, 상수를 찾지 못하면 오류를 남기고 건너뛴다.
        /// </summary>
        private static void DeleteSaveKey(Type owner, string fieldName)
        {
            FieldInfo field = owner.GetField(fieldName, PRIVATE_STATIC);
            if (field == null || !field.IsLiteral)
            {
                Debug.LogError($"SAVE RESET: {owner.Name}.{fieldName} key not found");
                return;
            }
            PlayerPrefs.DeleteKey((string)field.GetRawConstantValue());
        }

        /// <summary>
        /// owner 클래스의 private static 필드 fieldName을 null로 만든다.
        /// owner와 fieldName을 사용하며, 필드를 찾지 못하면 오류를 남기고 건너뛴다.
        /// </summary>
        private static void ClearStaticField(Type owner, string fieldName)
        {
            FieldInfo field = owner.GetField(fieldName, PRIVATE_STATIC);
            if (field == null)
            {
                Debug.LogError($"SAVE RESET: {owner.Name}.{fieldName} cache not found");
                return;
            }
            field.SetValue(null, null);
        }
    }
}
