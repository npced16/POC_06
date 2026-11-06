using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

using UnityEngine;
using UnityEngine.SceneManagement;

using IronRing.Contracts;
using IronRing.Contracts.Presentation;
using IronRing.Localization;

namespace IronRing.Save
{
    /// <summary>
    /// 스펙 003. 저장 슬롯 3개의 목록·저장·불러오기·새 게임 API.
    /// List와 HasAny는 타이틀 씬에서 GameServices 없이 불리므로 파일과 Strings만 쓴다.
    /// </summary>
    public static class SaveSlots
    {
        public const int SLOT_COUNT = 3;

        /// <summary>슬롯 수. 타이틀 슬롯 화면이 칸 수로 쓴다.</summary>
        public static int SlotCount => SLOT_COUNT;

        private const string FOLDER = "saves";
        private const string SUMMARY_TIME_FORMAT = "MM-dd HH:mm";

        private static readonly List<ISaveParticipant> _participants = new List<ISaveParticipant>();
        private static string _lastSavedState;
        private static bool _quitAllowed;
        private static bool _loadFailed;

        /// <summary>씬을 넘어 전달하는 불러올 슬롯 번호. 0이면 새 게임.</summary>
        public static int PendingLoadSlot { get; private set; }

        /// <summary>종료 허용 표시. 게임 종료 확인을 거쳤으면 true.</summary>
        public static bool QuitAllowed => _quitAllowed;

        /// <summary>불러온 슬롯을 참여자에 되돌리는 중이면 true. 복원이 내는 변경 이벤트의 토스트를 막는 데 쓴다.</summary>
        public static bool IsRestoring { get; private set; }

        /// <summary>도메인 재로드가 꺼져 있으므로 플레이 진입마다 정적 상태를 지운다.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _participants.Clear();
            _lastSavedState = null;
            _quitAllowed = false;
            _loadFailed = false;
            PendingLoadSlot = 0;
            IsRestoring = false;
        }

        /// <summary>
        /// 슬롯 1~SLOT_COUNT의 표시 정보를 돌려준다. 파일이 없으면 IsEmpty, 읽지 못하면 IsCorrupt를 세운다.
        /// GameServices를 쓰지 않는다.
        /// </summary>
        public static SaveSlotInfo[] List()
        {
            SaveSlotInfo[] result = new SaveSlotInfo[SLOT_COUNT];
            for (int i = 0; i < SLOT_COUNT; i++)
            {
                result[i] = ReadInfo(i + 1);
            }
            return result;
        }

        /// <summary>비어 있지도 손상되지도 않은 슬롯이 하나라도 있으면 true를 돌려준다.</summary>
        public static bool HasAny()
        {
            foreach (SaveSlotInfo info in List())
            {
                if (!info.IsEmpty && !info.IsCorrupt) return true;
            }
            return false;
        }

        /// <summary>
        /// 참여자 상태를 모아 slot 번호 파일에 쓴다. 임시 파일에 쓴 뒤 바꿔 끼운다.
        /// 성공하면 _lastSavedState를 지금 상태로 두고 true, 입출력 예외면 기존 파일을 두고 false를 돌려준다.
        /// </summary>
        public static bool Save(int slot)
        {
            if (slot < 1 || slot > SLOT_COUNT) return false;
            SaveEntry[] entries = CaptureEntries();
            SaveFile file = new SaveFile
            {
                SavedAt = DateTime.Now.ToString("o", CultureInfo.InvariantCulture),
                Header = BuildHeader(),
                Entries = entries,
            };
            string path = PathOf(slot);
            string temp = path + ".tmp";
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(temp, JsonUtility.ToJson(file));
                if (File.Exists(path)) File.Replace(temp, path, null);
                else File.Move(temp, path);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                Debug.LogWarning("SAVE: slot " + slot + " 저장 실패: " + e.Message);
                // 임시 파일 정리가 다시 실패해도 저장 실패 처리(false 반환)는 이어 간다.
                try
                {
                    if (File.Exists(temp)) File.Delete(temp);
                }
                catch (Exception cleanup) when (cleanup is IOException || cleanup is UnauthorizedAccessException)
                {
                    Debug.LogWarning("SAVE: 임시 파일 정리 실패: " + cleanup.Message);
                }
                return false;
            }
            _lastSavedState = Serialize(entries);
            Debug.Log("SAVE: slot " + slot + " 저장 (" + entries.Length + "개 항목)");
            return true;
        }

        /// <summary>불러올 슬롯을 PendingLoadSlot에 적고 Main 씬을 연다.</summary>
        public static void LoadAndStart(int slot)
        {
            PendingLoadSlot = slot;
            OpenMain();
        }

        /// <summary>PendingLoadSlot을 0으로 두고 Main 씬을 새 게임으로 연다.</summary>
        public static void StartNew()
        {
            PendingLoadSlot = 0;
            OpenMain();
        }

        /// <summary>순환 페이즈이거나, 지금 참여자 상태가 마지막 저장·불러오기·새 게임 시점과 다르면 true를 돌려준다.</summary>
        public static bool HasUnsavedProgress()
        {
            // 순환 중의 운행 진행은 저장 대상이 아니라 나가면 사라지므로 항상 저장 안 한 진행으로 본다.
            IPhaseLoop loop = GameServices.Get<IPhaseLoop>();
            if (loop != null && loop.Phase == LoopPhase.Circulate) return true;
            if (_lastSavedState == null) return false;
            return Serialize(CaptureEntries()) != _lastSavedState;
        }

        /// <summary>종료 허용 표시를 세운다. 이 뒤의 wantsToQuit은 막지 않는다.</summary>
        public static void AllowQuit()
        {
            _quitAllowed = true;
        }

        /// <summary>FR-019 실패 표시를 읽고 지운다. 다시 연 Main 씬에서 토스트를 띄울지 판단할 때 쓴다.</summary>
        public static bool ConsumeLoadFailed()
        {
            bool failed = _loadFailed;
            _loadFailed = false;
            return failed;
        }

        /// <summary>SaveInstaller가 모은 참여자 목록을 Order 순서로 받아 둔다.</summary>
        internal static void SetParticipants(IEnumerable<ISaveParticipant> participants)
        {
            _participants.Clear();
            _participants.AddRange(participants);
            _participants.Sort((a, b) => a.Order.CompareTo(b.Order));
        }

        /// <summary>지금 상태를 저장 기준으로 둔다(새 게임 시작 때).</summary>
        internal static void MarkCurrentAsSaved()
        {
            _lastSavedState = Serialize(CaptureEntries());
        }

        /// <summary>
        /// PendingLoadSlot 파일을 참여자 Order 순서로 Restore한다. 성공하면 true.
        /// 읽기·복원 중 예외가 나면 남은 복원을 멈추고 실패 표시를 세운 뒤 false를 돌려준다.
        /// </summary>
        internal static bool RestorePending()
        {
            int slot = PendingLoadSlot;
            PendingLoadSlot = 0;
            IsRestoring = true;
            try
            {
                SaveFile file = ReadFile(slot);
                if (file == null) throw new InvalidDataException("slot " + slot + " 파일 없음 또는 손상");
                Dictionary<string, string> byKey = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (SaveEntry entry in file.Entries) byKey[entry.Key] = entry.Json;
                foreach (ISaveParticipant participant in _participants)
                {
                    if (byKey.TryGetValue(participant.SaveKey, out string json)) participant.Restore(json);
                    else RestoreMissing(participant);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("SAVE: slot " + slot + " 불러오기 실패: " + e.Message);
                _loadFailed = true;
                return false;
            }
            finally
            {
                IsRestoring = false;
            }
            MarkCurrentAsSaved();
            Debug.Log("SAVE: slot " + slot + " 불러옴");
            return true;
        }

        /// <summary>
        /// 슬롯 파일에 키가 없는 participant를 처리한다(빠진 키 규칙). ISaveDefault면 기본값을 적용하고 로그를 남기며,
        /// 아니면 필수 키 누락으로 InvalidDataException을 던져 불러오기를 실패시킨다.
        /// </summary>
        private static void RestoreMissing(ISaveParticipant participant)
        {
            if (!(participant is ISaveDefault fallback)) throw new InvalidDataException("필수 키 " + participant.SaveKey + " 없음");
            fallback.RestoreDefault();
            Debug.Log("SAVE: " + participant.SaveKey + " 없음 → 기본값");
        }

        /// <summary>참여자마다 Capture를 불러 SaveEntry 배열을 만든다.</summary>
        private static SaveEntry[] CaptureEntries()
        {
            SaveEntry[] entries = new SaveEntry[_participants.Count];
            for (int i = 0; i < _participants.Count; i++)
            {
                entries[i] = new SaveEntry { Key = _participants[i].SaveKey, Json = _participants[i].Capture() };
            }
            return entries;
        }

        /// <summary>Entries를 비교용 문자열로 직렬화한다.</summary>
        private static string Serialize(SaveEntry[] entries)
        {
            return JsonUtility.ToJson(new SaveEntryList { Entries = entries });
        }

        /// <summary>등록된 서비스에서 마을 단계·동전·바퀴 수를 읽어 요약 헤더를 만든다.</summary>
        private static SaveHeader BuildHeader()
        {
            ITownTier tier = GameServices.Get<ITownTier>();
            IMoney money = GameServices.Get<IMoney>();
            IPhaseLoop loop = GameServices.Get<IPhaseLoop>();
            return new SaveHeader
            {
                TownTier = tier != null ? tier.Tier : 0,
                TownTierName = tier != null ? tier.TierName : string.Empty,
                Coins = money != null ? money.Balance : 0,
                Lap = loop != null ? loop.Lap : 0,
            };
        }

        /// <summary>slot 파일을 읽어 SaveSlotInfo로 바꾼다.</summary>
        private static SaveSlotInfo ReadInfo(int slot)
        {
            SaveSlotInfo info = new SaveSlotInfo { Slot = slot };
            if (!File.Exists(PathOf(slot)))
            {
                info.IsEmpty = true;
                return info;
            }
            SaveFile file = ReadFile(slot);
            if (file == null || !DateTime.TryParse(file.SavedAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime savedAt))
            {
                info.IsCorrupt = true;
                return info;
            }
            info.SavedAt = savedAt;
            info.TownTier = file.Header.TownTier;
            info.TownTierName = file.Header.TownTierName;
            info.Coins = file.Header.Coins;
            info.Lap = file.Header.Lap;
            info.Summary = string.Format(Strings.Get("save_slot_summary"), info.TownTier, info.Coins, savedAt.ToString(SUMMARY_TIME_FORMAT, CultureInfo.InvariantCulture));
            return info;
        }

        /// <summary>slot 파일을 읽어 SaveFile을 돌려준다. 없거나 해석하지 못하거나 버전이 높으면 null.</summary>
        private static SaveFile ReadFile(int slot)
        {
            try
            {
                SaveFile file = JsonUtility.FromJson<SaveFile>(File.ReadAllText(PathOf(slot)));
                if (file == null || file.Entries == null || file.Header == null) return null;
                if (file.Version > SaveFile.CURRENT_VERSION) return null;
                return file;
            }
            catch (Exception e) when (e is IOException || e is ArgumentException || e is UnauthorizedAccessException)
            {
                return null;
            }
        }

        /// <summary>slot 번호의 파일 경로를 돌려준다.</summary>
        private static string PathOf(int slot)
        {
            return Path.Combine(Application.persistentDataPath, FOLDER, "slot" + slot + ".json");
        }

        /// <summary>시간 배율을 1로 되돌리고 Main 씬을 연다.</summary>
        private static void OpenMain()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneNames.Main);
        }
    }

    /// <summary>
    /// 스펙 003 빠진 키 규칙. 슬롯 파일에 자기 키가 없어도 불러오기를 실패시키지 않는 참여자가 구현한다.
    /// </summary>
    internal interface ISaveDefault
    {
        /// <summary>슬롯에 키가 없을 때 명시적 기본값으로 상태를 바꾼다.</summary>
        void RestoreDefault();
    }
}
