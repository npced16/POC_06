using System.Collections;

using UnityEngine;

using IronRing.Contracts;
using IronRing.Contracts.Presentation;
using IronRing.Track;
using IronRing.Train.Equipment;

using RaidInstaller = IronRing.Combat.Raid.RaidInstaller;

namespace IronRing.Presentation.Travel.Demo
{
    /// <summary>
    /// 스토리 037 캡처용 데모. 인자 --travel-demo --travel-stage depart|halt|terminal|raid|return(브리지 extra args도 읽는다).
    /// 출발시키고 단계 시점에 Time.timeScale = 0으로 멈춘 뒤 "TRAVEL: capture-ready stage=…"를 남긴다(캡처는 코디네이터).
    /// raid 단계는 포탑 2 + 방벽판 1을 얹고, 달리는 중 습격 5초(게임 시간)에서 멈춘다. --travel-speed N(기본 2)으로 그 전까지 빨리 감는다.
    /// </summary>
    public sealed class TravelDemoDriver : MonoBehaviour
    {
        private const float DepartProgress = 0.05f;
        private const float RaidSeconds = 5f;
        private const float ReturnProgress = 0.9f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (!RaidInstaller.HasArgument("--travel-demo")) return;
            new GameObject("TravelDemoDriver").AddComponent<TravelDemoDriver>();
        }

        private IEnumerator Start()
        {
            string stage = RaidInstaller.Argument("--travel-stage") ?? "depart";
            float speed = float.TryParse(RaidInstaller.Argument("--travel-speed"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float s) ? s : 2f;
            Application.runInBackground = true;
            while (!GameServices.IsReady) yield return null;
            yield return null;
            TrainController train = FindFirstObjectByType<TrainController>();
            TravelSideView view = FindFirstObjectByType<TravelSideView>();
            var raid = RaidInstaller.System;
            if (train == null || view == null) { Debug.LogError("TRAVEL: demo needs TrainController and TravelSideView"); yield break; }
            if (stage == "raid") Equip();
            Time.timeScale = speed;
            train.Depart();
            Debug.Log($"TRAVEL: demo stage={stage} speed={speed}");
            RingTrack track = train.Track;
            switch (stage)
            {
                case "halt":
                    float halt = 0.36f;
                    foreach (TravelLandmark lm in view.Data._landmarks) if (lm._kind == "halt") halt = lm._progress;
                    // 폐역이 열차 바로 앞(머리에서 조금 앞)에 오는 진행도.
                    yield return new WaitUntil(() => track.IsMoving && track.CurrentNode.IsStation && view.Position >= halt - 0.04f);
                    break;
                case "terminal":
                    yield return new WaitUntil(() => !track.IsMoving && !track.CurrentNode.IsStation);
                    yield return null;
                    break;
                case "raid":
                    yield return new WaitUntil(() => raid != null && raid.ActiveEnroute && raid.Elapsed >= RaidSeconds);
                    break;
                case "return":
                    yield return new WaitUntil(() => !track.CurrentNode.IsStation);
                    while (!(track.IsMoving && !track.CurrentNode.IsStation && train.TravelProgress >= ReturnProgress))
                    {
                        // 종점 이벤트를 자동으로 넘긴다(HUD 클릭 대신).
                        if (GameServices.Get<IWaypointResolver>() is IronRing.Combat.WaypointResolver resolver && resolver.IsEncounterActive && !resolver.IsRaid) resolver.Advance();
                        yield return null;
                    }
                    break;
                default:
                    yield return new WaitUntil(() => track.IsMoving && view.Position >= DepartProgress);
                    break;
            }
            Time.timeScale = 0f;
            yield return null;
            Debug.Log($"TRAVEL: capture-ready stage={stage} position={view.Position:F2} facingRight={view.FacingRight} raid={(raid != null && raid.IsActive)} monsters={(raid != null ? raid.MonstersAlive : 0)}");
        }

        private static void Equip()
        {
            // 스토리 038: 데모 배치는 고철 없이(PlaceFree).
            TrainLoadout loadout = TrainEquipmentInstaller.Loadout;
            if (loadout == null) return;
            if (loadout.CountOf(TrainLoadout.TURRET) < 1) loadout.PlaceFree(0, 0, 1, TrainLoadout.TURRET);
            if (loadout.CountOf(TrainLoadout.TURRET) < 2) loadout.PlaceFree(2, 2, 1, TrainLoadout.TURRET);
            if (loadout.CountOf("armor_plate") < 1) loadout.PlaceFree(1, 1, 0, "armor_plate");
        }
    }
}
