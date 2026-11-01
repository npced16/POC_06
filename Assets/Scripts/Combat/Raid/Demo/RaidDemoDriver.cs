using System.Collections;

using UnityEngine;

using IronRing.Contracts.Presentation;
using IronRing.Track;

namespace IronRing.Combat.Raid.Demo
{
    /// <summary>
    /// 스토리 033 캡처 데모(--raid-demo --raid-stage start|mid|win|lose). 출발 → 종점 도착 → 습격(습격 모드라 항상 습격)을 자동 사격만으로 진행하고,
    /// 단계 시점에 Time.timeScale = 0으로 멈춰 화면을 고정한 뒤 "RAID: capture-ready stage=…"를 남긴다(캡처는 코디네이터).
    /// start: 습격 직후(카메라 확대·둥지·첫 몬스터), mid: 데이터 초 뒤(포탑 2·방벽 1), win: 강한 포탑으로 통과 끝·승리 토스트, lose: 포탑 0·낮은 내구도로 패배.
    /// </summary>
    public sealed class RaidDemoDriver : MonoBehaviour
    {
        private RaidSystem _raid;
        private string _stage;
        private bool _frozen;
        private bool _ended;

        /// <summary>습격 규칙과 단계 이름을 받는다. 단계에 맞춰 장비를 강제한다.</summary>
        public void Bind(RaidSystem raid, string stage)
        {
            _raid = raid;
            _stage = stage;
            RaidData data = raid.Data;
            switch (stage)
            {
                case "win": raid.OverrideTurrets = data.DemoTurretsWin; raid.OverrideWalls = data.DemoWallsWin; break;
                case "lose": raid.OverrideTurrets = 0; raid.OverrideWalls = 0; raid.OverrideHullMax = data.DemoHullLose; break;
                default: raid.OverrideTurrets = data.DemoTurretsMid; raid.OverrideWalls = data.DemoWallsMid; break;
            }
            raid.RaidEnded += OnEnded;
            UnityEngine.Debug.Log($"RAID: demo stage={stage}");
        }

        private IEnumerator Start()
        {
            Application.runInBackground = true;
            while (!GameServices.IsReady) yield return null;
            yield return null;
            TrainController train = FindFirstObjectByType<TrainController>();
            if (train != null) train.Depart();
        }

        private void Update()
        {
            if (_frozen || _raid == null || !_raid.IsActive) return;
            if (_stage == "start" && _raid.Elapsed >= _raid.Data.DemoStartSeconds) Freeze();
            else if (_stage == "mid" && _raid.Elapsed >= _raid.Data.DemoMidSeconds) Freeze();
        }

        private void OnEnded(bool victory)
        {
            if (_ended) return;
            _ended = true;
            if (_stage == "win" || _stage == "lose") StartCoroutine(FreezeAfterToast());
        }

        private IEnumerator FreezeAfterToast()
        {
            yield return new WaitForSecondsRealtime(0.5f);
            Freeze();
        }

        private void Freeze()
        {
            _frozen = true;
            Time.timeScale = 0f;
            UnityEngine.Debug.Log($"RAID: capture-ready stage={_stage} active={_raid.IsActive} hull={_raid.Hull}/{_raid.HullMax} monsters={_raid.MonstersAlive} nests={_raid.NestsAlive} t={_raid.Elapsed:F1}");
        }

        private void OnDestroy()
        {
            if (_raid != null) _raid.RaidEnded -= OnEnded;
            if (_frozen) Time.timeScale = 1f;
        }
    }
}
