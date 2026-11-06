using System;
using System.Collections.Generic;

using UnityEngine;

using IronRing.Combat;
using IronRing.Contracts;
using IronRing.Contracts.Presentation;

namespace IronRing.Save
{
    /// <summary>
    /// 스펙 003. GameServices.Ready 뒤에 저장 참여자를 모으고, 불러올 슬롯이 있으면 복원하고,
    /// Application.wantsToQuit으로 저장하지 않은 진행의 종료를 가로챈다.
    /// </summary>
    public static class SaveInstaller
    {
        private static bool _quitHooked;

        /// <summary>저장하지 않은 진행이 있어 창 닫기를 막았을 때 발생한다. 게임 메뉴가 종료 확인 창을 연다.</summary>
        public static event Action QuitBlocked;

        /// <summary>종료 확인 창을 지금 보여 줄 수 있는지 묻는 함수. 게임 메뉴가 넣는다. 없거나 false면 종료를 막지 않는다.</summary>
        public static Func<bool> CanPromptQuit { get; set; }

        /// <summary>도메인 재로드가 꺼져 있으므로 플레이 진입마다 정적 상태를 지운다.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            GameServices.Composing -= OnComposing;
            GameServices.Ready -= OnReady;
            if (_quitHooked) Application.wantsToQuit -= OnWantsToQuit;
            _quitHooked = false;
            QuitBlocked = null;
            CanPromptQuit = null;
        }

        /// <summary>MainRoot의 GameServices.Reset()보다 뒤에 구독하도록 씬 로드 직전에 건다.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Subscribe()
        {
            SubscribeComposing();
            if (!_quitHooked && !IsDemoRun())
            {
                Application.wantsToQuit += OnWantsToQuit;
                _quitHooked = true;
            }
        }

        /// <summary>
        /// GameServices.Composing을 중복 없이 구독한다. 첫 진입은 Subscribe가, Main 재진입(FR-021)은 MainSceneReentry가 부른다.
        /// 구독 상태만 바꾼다.
        /// </summary>
        internal static void SubscribeComposing()
        {
            GameServices.Composing -= OnComposing;
            GameServices.Composing += OnComposing;
        }

        /// <summary>
        /// Composing 안에서 Ready를 구독해 Ready 구독 목록의 맨 뒤에 둔다. 경제 설치기가 Ready에서 만드는 주민·마을 등급까지
        /// 등록된 뒤에 참여자를 모으고 복원하기 위해서다. Ready 구독 상태만 바꾼다.
        /// </summary>
        private static void OnComposing()
        {
            GameServices.Ready -= OnReady;
            GameServices.Ready += OnReady;
        }

        /// <summary>
        /// 참여자를 모아 SaveSlots에 넘기고, PendingLoadSlot이 있으면 복원한다.
        /// 복원에 실패하면 새 게임으로 Main을 다시 연다. 대기 슬롯이 없으면 지금 상태를 저장 기준으로 둔다.
        /// </summary>
        private static void OnReady()
        {
            SaveSlots.SetParticipants(CollectParticipants());
            if (SaveSlots.PendingLoadSlot <= 0)
            {
                // 정적 클래스라 씬을 다시 열어도 남는 포탑·단서 기록을 새 게임 상태로 비운다.
                TrainArmament.ResetForNewGame();
                BattleHintJournal.ResetForNewGame();
                SaveSlots.MarkCurrentAsSaved();
                return;
            }
            if (!SaveSlots.RestorePending()) SaveSlots.StartNew();
        }

        /// <summary>
        /// 저장 대상 계약 타입마다 등록된 서비스와 씬의 MonoBehaviour(FR-020)를 모아 ISaveParticipant인 것만 중복 없이 돌려준다.
        /// GameServices에는 전체 목록 조회가 없어서 타입 목록으로 찾는다.
        /// </summary>
        private static List<ISaveParticipant> CollectParticipants()
        {
            object[] services =
            {
                GameServices.Get<IHubInterior>(),
                GameServices.Get<IResourceInventory>(),
                GameServices.Get<IItemInventory>(),
                GameServices.Get<ITileStock>(),
                GameServices.Get<IItemPlacement>(),
                GameServices.Get<IHabitatClusters>(),
                GameServices.Get<IHabitatCodex>(),
                GameServices.Get<INpcRecruiter>(),
                GameServices.Get<INpcHomes>(),
                GameServices.Get<IPassengers>(),
                GameServices.Get<ILineOpener>(),
                GameServices.Get<IHubStateSystem>(),
                GameServices.Get<IPopulation>(),
                GameServices.Get<ITownTier>(),
                GameServices.Get<IMoney>(),
                GameServices.Get<ILedger>(),
                GameServices.Get<ITrackRuntime>(),
                GameServices.Get<IPhaseLoop>(),
            };
            List<ISaveParticipant> result = new List<ISaveParticipant>();
            foreach (object service in services)
            {
                if (service is ISaveParticipant participant && !result.Contains(participant)) result.Add(participant);
            }
            // FR-020: 서비스가 아닌 씬 화면(퀵슬롯 등)도 참여자로 모은다.
            foreach (MonoBehaviour behaviour in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
            {
                if (behaviour is ISaveParticipant participant && !result.Contains(participant)) result.Add(participant);
            }
            // 서비스가 아닌 정적 클래스(포탑·전투 단서)는 래퍼로 참여시킨다.
            result.Add(new TrainArmamentSaveParticipant());
            result.Add(new BattleHintJournalSaveParticipant());
            return result;
        }

        /// <summary>
        /// 창 닫기·Alt+F4 때 불린다. 종료 허용 표시가 없고 저장하지 않은 진행이 있으면 QuitBlocked를 내고 false로 막는다.
        /// </summary>
        private static bool OnWantsToQuit()
        {
            if (SaveSlots.QuitAllowed || !GameServices.IsReady || !SaveSlots.HasUnsavedProgress()) return true;
            // 전투 씬처럼 확인 창을 보여 줄 수 없으면 보이지 않는 창에 갇히지 않도록 종료를 막지 않는다.
            if (CanPromptQuit == null || !CanPromptQuit()) return true;
            QuitBlocked?.Invoke();
            return false;
        }

        /// <summary>데모 인자(--*-demo)로 실행했으면 true. 데모 자동 종료를 막지 않기 위해 쓴다.</summary>
        private static bool IsDemoRun()
        {
            foreach (string arg in Environment.GetCommandLineArgs())
            {
                if (arg.StartsWith("--", StringComparison.Ordinal) && arg.EndsWith("-demo", StringComparison.Ordinal)) return true;
            }
            return false;
        }
    }
}
