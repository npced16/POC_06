using System;

namespace IronRing.Save
{
    /// <summary>스펙 003. 슬롯 파일 하나의 직렬화 형식.</summary>
    [Serializable]
    public sealed class SaveFile
    {
        public const int CURRENT_VERSION = 1;

        public int Version = CURRENT_VERSION;
        public string SavedAt;
        public SaveHeader Header = new SaveHeader();
        public SaveEntry[] Entries = Array.Empty<SaveEntry>();
    }

    /// <summary>스펙 003. 슬롯 칸 요약에 쓰는 값.</summary>
    [Serializable]
    public sealed class SaveHeader
    {
        public int TownTier;
        public string TownTierName;
        public int Coins;
        public int Lap;
    }

    /// <summary>스펙 003. 참여 시스템 하나의 상태.</summary>
    [Serializable]
    public sealed class SaveEntry
    {
        public string Key;
        public string Json;
    }

    /// <summary>스펙 003. 저장 상태 비교용으로 Entries만 감싼다.</summary>
    [Serializable]
    internal sealed class SaveEntryList
    {
        public SaveEntry[] Entries;
    }

    /// <summary>스펙 003. 타이틀·게임 메뉴가 슬롯 칸에 보여 주는 정보.</summary>
    public sealed class SaveSlotInfo
    {
        /// <summary>슬롯 번호(1부터).</summary>
        public int Slot { get; internal set; }
        /// <summary>파일이 없으면 true.</summary>
        public bool IsEmpty { get; internal set; }
        /// <summary>파일을 읽거나 해석하지 못하면 true.</summary>
        public bool IsCorrupt { get; internal set; }
        /// <summary>저장 시각(현지 시각). 비었거나 손상이면 default.</summary>
        public DateTime SavedAt { get; internal set; }
        /// <summary>저장 때 마을 단계.</summary>
        public int TownTier { get; internal set; }
        /// <summary>저장 때 마을 단계 이름.</summary>
        public string TownTierName { get; internal set; }
        /// <summary>저장 때 동전.</summary>
        public int Coins { get; internal set; }
        /// <summary>저장 때 바퀴 수.</summary>
        public int Lap { get; internal set; }
        /// <summary>칸에 그대로 쓰는 한 줄 요약(save_slot_summary 형식). 비었거나 손상이면 빈 문자열.</summary>
        public string Summary { get; internal set; } = string.Empty;
    }
}
