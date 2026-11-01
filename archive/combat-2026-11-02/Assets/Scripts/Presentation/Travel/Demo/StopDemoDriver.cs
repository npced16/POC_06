using System.Collections;

using UnityEngine;

using IronRing.Contracts.Presentation;
using IronRing.Track;
using IronRing.Train.Equipment;
using IronRing.Train.Scrap;

using RaidInstaller = IronRing.Combat.Raid.RaidInstaller;

namespace IronRing.Presentation.Travel.Demo
{
    /// <summary>
    /// 스토리 038 데모(최소). 인자 --stop-demo --stop-stage arrive|farm|wave|depart [--travel-speed N(기본 4)].
    /// 포탑 1·방벽판 1은 고철로 사고(SCRAP: spend), 포탑 1은 무료로 얹은 뒤 출발해 빨리 감고, 중간역 정차 디펜스에서
    /// arrive = 0.4초, farm = 인부 1명 보내고 3초, wave = 인부 2명 + 버틴 8초 뒤 포탑 발사 직후, depart = 데이터 _demoHoldSeconds 뒤 Leave 직전에
    /// Time.timeScale = 0으로 멈추고 "STOP: capture-ready stage=…"를 남긴다.
    /// </summary>
    public sealed class StopDemoDriver : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (!RaidInstaller.HasArgument("--stop-demo")) return;
            new GameObject("StopDemoDriver").AddComponent<StopDemoDriver>();
        }

        private IEnumerator Start()
        {
            string stage = RaidInstaller.Argument("--stop-stage") ?? "arrive";
            float speed = float.TryParse(RaidInstaller.Argument("--travel-speed"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float s) ? s : 4f;
            Application.runInBackground = true;
            while (!GameServices.IsReady) yield return null;
            yield return null;
            TrainController train = FindFirstObjectByType<TrainController>();
            var raid = RaidInstaller.System;
            ScrapFarming farming = ScrapInstaller.Farming;
            TrainLoadout loadout = TrainEquipmentInstaller.Loadout;
            if (train == null || raid == null || farming == null || !StopViewMode.IsTop) { Debug.LogError("STOP: demo needs TrainController, RaidSystem, ScrapFarming and _stopView top"); yield break; }
            if (loadout != null)
            {
                loadout.TryPlace(0, 0, 1, TrainLoadout.TURRET);
                loadout.TryPlace(1, 1, 0, "armor_plate");
                loadout.PlaceFree(2, 2, 1, TrainLoadout.TURRET);
            }
            Time.timeScale = speed;
            train.Depart();
            Debug.Log($"STOP: demo stage={stage} speed={speed}");
            yield return new WaitUntil(() => raid.ActiveStop && farming.IsActive);
            float lastGun = -1f;
            raid.GunFired += (gun, x, y) => lastGun = raid.Elapsed;
            switch (stage)
            {
                case "farm":
                    farming.Dispatch(0);
                    yield return new WaitUntil(() => raid.Elapsed >= 3f);
                    break;
                case "wave":
                    farming.Dispatch(0); farming.Dispatch(3);
                    yield return new WaitUntil(() => !raid.ActiveStop || (raid.Elapsed >= 8f && lastGun >= 0f && raid.Elapsed - lastGun < 0.1f));
                    break;
                case "depart":
                    farming.Dispatch(0); farming.Dispatch(3);
                    yield return new WaitUntil(() => !raid.ActiveStop || raid.Elapsed >= farming.Data.DemoHoldSeconds);
                    break;
                default:
                    yield return new WaitUntil(() => raid.Elapsed >= 0.4f);
                    break;
            }
            Time.timeScale = 0f;
            yield return null;
            var scrap = GameServices.Get<IronRing.Contracts.IScrap>();
            Debug.Log($"STOP: capture-ready stage={stage} held={raid.Elapsed:F1}s wave={raid.StopWave} hull={raid.Hull}/{raid.HullMax} monsters={raid.MonstersAlive} scrap={scrap?.Amount ?? 0} carried={farming.Carried} busy={farming.Busy}");
        }
    }
}
