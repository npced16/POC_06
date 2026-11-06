using System.Collections.Generic;

using UnityEngine;

using IronRing.Combat;
using IronRing.Contracts;
using IronRing.Contracts.Presentation;
using IronRing.Habitats;
using IronRing.Npc;
using IronRing.Presentation.Interior;
using IronRing.Track;

namespace IronRing.Onboarding
{
    /// <summary>
    /// 스펙 001 FR-015, FR-016. 개발자 메뉴에서 고른 학습 항목의 본 기록을 지우고, 팝업이 뜨기 직전 상황을 공개 서비스로 만든다.
    /// 팝업은 실제 게임 이벤트나 플레이어 행동으로 감지되어 떠야 하므로 강제로 띄우지 않는다.
    /// 상황을 만들 수 없을 때만 로그에 fallback을 남기고 팝업을 강제로 띄운다.
    /// </summary>
    public static class OnboardingSituations
    {
        private const string COBBLE = "tile_cobble";
        private const int DEV_COINS = 500;
        private const int LOT_SIZE = 4;
        private const int MAX_LAP_STEPS = 20;
        private static readonly string[] HOTBAR_FILL_TYPES =
        {
            "cleared_lot", "logging_site", "stone_well", "terrace_field", "coal_shaft", "iron_forge", "copper_pit", "steam_boiler",
        };

        /// <summary>
        /// topicId 항목의 본 기록을 지우고(Director.Rehearse) 팝업 직전 상황을 만든다. 로그 "ONBOARDING DEV:"에 플레이어가 할 행동을 남긴다.
        /// 상황을 만들지 못한 항목만 OnboardingDirector.ForceShow(topicId)로 팝업을 강제로 띄운다. Director가 없거나 모르는 id면 아무것도 하지 않는다.
        /// </summary>
        public static void Run(string topicId)
        {
            OnboardingDirector director = OnboardingInstaller.Director;
            if (director == null || director.Find(topicId) == null) return;
            director.Rehearse(topicId);
            string hubId = FirstHubId();
            string next = topicId switch
            {
                "select_material" => ReopenInterior(hubId, director, topicId) ? "자동" : null,
                "place_material" => OpenInterior(hubId) && Select(null) ? "퀵슬롯에서 재료를 고르세요" : null,
                "build_combo" => OpenInterior(hubId) && Coins() && PlaceLot(hubId) ? "자동(개간지 완성)" : null,
                "unlock_material" => OpenInterior(hubId) && PlaceLot(hubId) ? "자동(개간지 완성)" : null,
                "buy_material" => OpenInterior(hubId) && Select(null) ? "땅이 아닌 재료를 고르세요" : null,
                "pick_up" => OpenInterior(hubId) && Select(null) ? "재고가 있는 재료를 칸에 놓으세요" : null,
                "undo" => OpenInterior(hubId) && Coins() && Seen("pick_up") && PlaceAnywhere(hubId, COBBLE) ? "놓인 땅을 우클릭으로 회수하세요" : null,
                "camera_zoom" => OpenInterior(hubId) && Trigger(director, topicId) ? "자동" : null,
                "codex" => RevealHint() ? "자동(단서 공개)" : null,
                "depart" => OpenInterior(hubId) && Coins() && PlaceLot(hubId) ? "마을 내부를 닫으세요" : null,
                "clear_slot" => OpenInterior(hubId) && FillHotbar() ? "자동(퀵슬롯 9칸 채움)" : null,
                "passengers" => BoardPassenger() ? "자동(승객 탑승)" : null,
                "return_town" => FinishLap() ? "자동(한 바퀴 돌아 황동골 도착)" : null,
                "line_open" => OpenLockedHub() ? "자동(잿골 개통)" : null,
                "run_danger" => DropProsperity(hubId) ? "자동(번영 하락)" : null,
                _ => null,
            };
            if (next != null)
            {
                Debug.Log("ONBOARDING DEV: " + topicId + " ready -> " + next);
                return;
            }
            Debug.Log("ONBOARDING DEV: " + topicId + " fallback (상황을 만들 수 없어 팝업만 띄움)");
            director.ForceShow(topicId);
        }

        /// <summary>마을 내부가 닫혀 있으면 열어 실제 열림 이벤트로 발생시키고, 이미 열려 있으면 director에 topicId를 직접 발생시킨다. 처리했으면 true.</summary>
        private static bool ReopenInterior(string hubId, OnboardingDirector director, string topicId)
        {
            IViewMode view = GameServices.Get<IViewMode>();
            if (view != null && view.InteriorOpen) return Trigger(director, topicId);
            return OpenInterior(hubId);
        }

        /// <summary>director에 topicId 발생을 알린다. 이벤트가 따로 없는 이어짐 항목용. 항상 true.</summary>
        private static bool Trigger(OnboardingDirector director, string topicId)
        {
            director.Trigger(topicId);
            return true;
        }

        /// <summary>topicId 항목을 본 것으로 기록한다(앞 단계가 끝난 상태 만들기). 항상 true.</summary>
        private static bool Seen(string topicId)
        {
            OnboardingProgress.MarkSeen(topicId);
            return true;
        }

        /// <summary>
        /// 아직 짓지 않은 건물들을 지은 것으로 기록해 재료를 해금한다(CodexUnlocks.json 해금표 순서).
        /// 퀵슬롯이 새 재료를 빈 칸에 자동 배정해 9칸이 차면 실제 감지로 안내가 뜬다. 도감이 있으면 true.
        /// </summary>
        private static bool FillHotbar()
        {
            HabitatCodex codex = HabitatsInstaller.Codex;
            if (codex == null) return false;
            foreach (string typeId in HOTBAR_FILL_TYPES) codex.MarkBuilt(typeId);
            return true;
        }

        /// <summary>합류하지 않았고 타고 있지 않은 승객 후보 하나를 강제로 태워 실제 탑승 이벤트를 낸다. 태웠으면 true.</summary>
        private static bool BoardPassenger()
        {
            IPassengers passengers = GameServices.Get<IPassengers>();
            string npcId = FreeNpc();
            return passengers != null && npcId != null && passengers.TryBoard(npcId, Passengers.DemoWaypoint, true);
        }

        /// <summary>
        /// 열차를 출발시켜 노드마다 도착 처리(사건·전투 보류 해제 포함)를 반복해 황동골(노드 0)로 돌아오게 한다.
        /// 실제 바퀴 완료·도착 이벤트가 나서 마을 도착 상태가 된다. 돌아왔으면 true.
        /// </summary>
        private static bool FinishLap()
        {
            if (GameServices.Get<ITrackRuntime>() is not RingTrack track) return false;
            for (int step = 0; step < MAX_LAP_STEPS; step++)
            {
                ReleaseHolds();
                if (!track.IsMoving && !track.TryDepart()) return false;
                track.Arrive();
                if (track.CurrentIndex == 0 && !track.IsMoving) return true;
            }
            return false;
        }

        /// <summary>휴식지 사건이 열려 있으면 첫 선택지로 끝내고, 전투 조우가 있으면 넘겨 열차 정지 요청을 푼다.</summary>
        private static void ReleaseHolds()
        {
            PhaseLoop loop = LoopInstaller.Loop;
            if (loop != null && loop.IsActive)
            {
                loop.Choose(loop.FirstAvailable());
                loop.Continue();
            }
            if (GameServices.Get<IWaypointResolver>() is WaypointResolver resolver && resolver.IsEncounterActive) resolver.Advance();
        }


        /// <summary>합류하지 않았고 타고 있지 않은 승객 후보 하나를 hubId 마을에 합류시켜 실제 합류 이벤트를 낸다. 합류했으면 true.</summary>
        private static bool JoinNpc(string hubId)
        {
            string npcId = FreeNpc();
            return npcId != null && GameServices.Get<INpcRecruiter>() is NpcRecruiter recruiter && recruiter.TryJoin(npcId, hubId);
        }

        /// <summary>hubId 마을에 주민이 없으면 한 명 합류시킨다. 주민이 있으면 true.</summary>
        private static bool EnsureResident(string hubId)
        {
            INpcRecruiter recruiter = GameServices.Get<INpcRecruiter>();
            if (recruiter == null) return false;
            IReadOnlyList<string> residents = recruiter.NpcsAt(hubId);
            return (residents != null && residents.Count > 0) || JoinNpc(hubId);
        }

        /// <summary>NPC 목록에서 승객 프로필이 있고, 합류하지 않았으며, 지금 타고 있지 않은 첫 NPC id를 돌려준다. 없으면 null.</summary>
        private static string FreeNpc()
        {
            NpcPoolData pool = NpcInstaller.Pool;
            IPassengers passengers = GameServices.Get<IPassengers>();
            NpcRecruiter recruiter = GameServices.Get<INpcRecruiter>() as NpcRecruiter;
            if (pool == null) return null;
            foreach (NpcDefinition npc in pool.Definitions)
            {
                if (npc.Passenger == null || (recruiter != null && recruiter.IsJoined(npc.Id))) continue;
                if (passengers != null && passengers.PassengerIds != null && Contains(passengers.PassengerIds, npc.Id)) continue;
                return npc.Id;
            }
            return null;
        }

        /// <summary>ids에 id가 있으면 true.</summary>
        private static bool Contains(IReadOnlyList<string> ids, string id)
        {
            for (int i = 0; i < ids.Count; i++) if (ids[i] == id) return true;
            return false;
        }

        /// <summary>hubId 마을 번영도를 1 낮춰 실제 번영 변화 이벤트를 낸다. 낮췄으면 true.</summary>
        private static bool DropProsperity(string hubId)
        {
            IHubStateSystem hubs = GameServices.Get<IHubStateSystem>();
            if (hubs == null || hubId == null) return false;
            hubs.ApplyDelta(hubId, -1f, "onboarding_dev");
            return true;
        }

        /// <summary>번영 시스템의 첫 마을 id를 돌려준다. 없으면 null.</summary>
        private static string FirstHubId()
        {
            IReadOnlyList<string> hubs = GameServices.Get<IHubStateSystem>()?.HubIds;
            return hubs != null && hubs.Count > 0 ? hubs[0] : null;
        }

        /// <summary>hubId 마을 내부를 연다. 열었으면 true.</summary>
        private static bool OpenInterior(string hubId)
        {
            HubInteriorView view = Object.FindAnyObjectByType<HubInteriorView>();
            if (view == null || hubId == null) return false;
            view.Open(hubId);
            return true;
        }

        /// <summary>배치 고스트의 선택을 itemId 재료로 바꾼다. itemId가 null이면 선택을 비운다. 고스트가 있으면 true.</summary>
        private static bool Select(string itemId)
        {
            PlacementGhost ghost = PlacementGhost.Instance;
            if (ghost == null) return false;
            if (itemId == null) ghost.ClearSelection();
            else ghost.Select(new PlacementEntry(PlacementKind.Item, itemId));
            return true;
        }

        /// <summary>
        /// hubId 마을에서 안내 칸 규칙으로 고른 가로 빈 칸 3개에 땅을 실제로 놓아 새 개간지를 완성한다.
        /// 게임의 완성 처리(HabitatCompleted, 첫 완성이면 돌 해금)가 그대로 이어진다. 3칸을 다 놓았으면 true.
        /// </summary>
        private static bool PlaceLot(string hubId)
        {
            OnboardingGuide guide = OnboardingGuide.Active;
            PlacementGhost ghost = PlacementGhost.Instance;
            List<int> run = guide?.FreeRun(hubId, LOT_SIZE);
            if (ghost == null || run == null) return false;
            // 안내 배치 중이면 놓기 제한이 걸려 있으므로 먼저 끈다.
            guide.Stop();
            var entry = new PlacementEntry(PlacementKind.Item, COBBLE);
            foreach (int cell in run) if (!ghost.TryPlace(hubId, cell, entry)) return false;
            return true;
        }

        /// <summary>개발용 동전 DEV_COINS개를 지갑에 넣는다. 넣었으면 true.</summary>
        private static bool Coins()
        {
            IMoney money = GameServices.Get<IMoney>();
            return money != null && money.TryGain(DEV_COINS, "onboarding_dev");
        }

        /// <summary>
        /// hubId 마을에서 itemId를 놓을 수 있는 첫 빈 칸에 배치 고스트로 놓는다.
        /// 놓았으면 true.
        /// </summary>
        private static bool PlaceAnywhere(string hubId, string itemId)
        {
            PlacementGhost ghost = PlacementGhost.Instance;
            IReadOnlyList<IInteriorCell> cells = GameServices.Get<IHubInterior>()?.Cells(hubId);
            if (ghost == null || cells == null) return false;
            var entry = new PlacementEntry(PlacementKind.Item, itemId);
            for (int i = 0; i < cells.Count; i++)
            {
                if (cells[i].State != InteriorCellState.Empty || !ghost.CanPlace(hubId, i, entry)) continue;
                if (!ghost.TryPlace(hubId, i, entry)) return false;
                return true;
            }
            return false;
        }

        /// <summary>아직 짓지 않은 건물의 설계 단서 하나를 공개한다. 도감이 있으면 true.</summary>
        private static bool RevealHint()
        {
            if (GameServices.Get<IHabitatCodex>() is not HabitatCodex codex) return false;
            // 공개할 단서가 남아 있지 않으면 이벤트가 오지 않아 연습이 멈추므로 실패로 돌려 fallback을 태운다.
            int before = codex.HintedTypeIds.Count;
            codex.OnLapCompleted(0);
            return codex.HintedTypeIds.Count > before;
        }

        /// <summary>잠긴 마을 중 첫 마을(잿골)의 선로를 연다. 열 마을이 있으면 true.</summary>
        private static bool OpenLockedHub()
        {
            ILineOpener opener = GameServices.Get<ILineOpener>();
            if (opener == null || opener.LockedHubIds == null || opener.LockedHubIds.Count == 0) return false;
            opener.Open(opener.LockedHubIds[0]);
            return true;
        }
    }
}
