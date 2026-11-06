using System;

using UnityEngine;

using IronRing.Combat;
using IronRing.Contracts;

namespace IronRing.Save
{
    /// <summary>스펙 003. 정적 클래스 TrainArmament(열차 포탑)를 저장 슬롯에 참여시키는 래퍼.</summary>
    // 스펙 003: 빠진 키 기본값 ISaveDefault 구현 추가
    internal sealed class TrainArmamentSaveParticipant : ISaveParticipant, ISaveDefault
    {
        /// <summary>슬롯 파일 안의 키.</summary>
        public string SaveKey => "trainArmament";

        /// <summary>복원 순서(돈·장부 다음).</summary>
        public int Order => 85;

        /// <summary>포탑 장비를 JSON으로 꺼낸다.</summary>
        public string Capture() => TrainArmament.CaptureState();

        /// <summary>json으로 포탑 장비를 되돌린다.</summary>
        public void Restore(string json) => TrainArmament.RestoreState(json);

        /// <summary>슬롯에 키가 없을 때 포탑 장비를 새 게임 상태로 되돌린다(빠진 키 규칙).</summary>
        public void RestoreDefault() => TrainArmament.ResetForNewGame();
    }

    /// <summary>스펙 003. 정적 클래스 BattleHintJournal(전투 단서 기록)을 저장 슬롯에 참여시키는 래퍼.</summary>
    // 스펙 003: 빠진 키 기본값 ISaveDefault 구현 추가
    internal sealed class BattleHintJournalSaveParticipant : ISaveParticipant, ISaveDefault
    {
        /// <summary>슬롯 파일 안의 키.</summary>
        public string SaveKey => "battleHintJournal";

        /// <summary>복원 순서(포탑 다음).</summary>
        public int Order => 86;

        /// <summary>단서 기록을 기록 순서 그대로 JSON으로 꺼낸다.</summary>
        public string Capture()
        {
            string[] ids = new string[BattleHintJournal.All().Count];
            for (int i = 0; i < ids.Length; i++) ids[i] = BattleHintJournal.All()[i];
            return JsonUtility.ToJson(new HintSave { Ids = ids });
        }

        /// <summary>json으로 단서 기록을 통째로 되돌린다.</summary>
        public void Restore(string json)
        {
            HintSave state = JsonUtility.FromJson<HintSave>(json);
            BattleHintJournal.RestoreState(state.Ids);
        }

        /// <summary>슬롯에 키가 없을 때 단서 기록을 새 게임 상태로 비운다(빠진 키 규칙).</summary>
        public void RestoreDefault() => BattleHintJournal.ResetForNewGame();

        /// <summary>저장 형식: 기록된 단서 id 목록.</summary>
        [Serializable]
        private sealed class HintSave
        {
            public string[] Ids;
        }
    }
}
